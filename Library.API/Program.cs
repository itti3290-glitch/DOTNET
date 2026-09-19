using System.Text.Json.Serialization;
using Library.API.Middlewares;
using Library.API.Security;
using Library.Data;   // Only here, for DI registration.
using Library.Service;
using Microsoft.OpenApi;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// NLog replaces the default providers; application code keeps using the standard ILogger.
builder.Logging.ClearProviders();
builder.Host.UseNLog();

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

// Order matters: correlation id first (everything after it is tagged), then request logging,
// then exception handling, so the logged status is the final one.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(); // Before authentication so preflight requests and 401/403 responses carry CORS headers.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
