using System.Text.Json.Serialization;
using Library.API.Middlewares;
using Library.API.Security;
using Library.Data;   // Only here, for DI registration.
using Library.Service;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Render and most container hosts pick the port and inject it as PORT, then terminate TLS at
// their edge and forward plain HTTP inwards. Its presence is what distinguishes "running behind
// a platform proxy" from a local run, which decides the HTTPS redirect below.
var injectedPort = Environment.GetEnvironmentVariable("PORT");
var behindTlsProxy = !string.IsNullOrWhiteSpace(injectedPort);
if (behindTlsProxy)
    builder.WebHost.UseUrls($"http://0.0.0.0:{injectedPort}");

// NLog replaces the default providers; application code keeps using the standard ILogger.
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Trust the proxy's X-Forwarded-* so the scheme and client IP are the caller's, not the proxy's.
// The known-proxy lists are cleared because the platform's internal addresses are not fixed.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddUniformValidationResponse();

// CORS: only the origins listed in configuration (Cors:AllowedOrigins), e.g. the React dev server.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // Lets browser code read these response headers.
    .WithExposedHeaders("X-Correlation-Id", "Location")));

builder.Services.AddDataLayer(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection."));
builder.Services.AddServiceLayer();
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Library API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/auth/login."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// A deployed container has no shell step to run `dotnet ef database update`, so the schema is
// brought up here. Migrate() is a no-op once the database is current. Turn it off with
// Database:MigrateOnStartup=false if migrations are applied out of band instead.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
}

// Must come before anything that reads the scheme or the client IP.
app.UseForwardedHeaders();

// Order matters: correlation id first (everything after it is tagged), then request logging,
// then exception handling, so the logged status is the final one.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Off outside Development unless Swagger:Enabled says otherwise, which is how the deployed
// demo keeps its API browsable.
if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Behind a TLS-terminating proxy the hop into the container is plain HTTP by design, and the
// platform's health checks do not follow redirects, so redirecting here would break them.
if (!behindTlsProxy)
    app.UseHttpsRedirection();

app.UseCors(); // Before authentication so preflight requests and 401/403 responses carry CORS headers.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Unauthenticated liveness probe for the platform; deliberately cheap and outside /api.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
