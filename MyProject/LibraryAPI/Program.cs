using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger / OpenAPI. AddSwaggerGen scans the controllers and builds the OpenAPI
// document; UseSwaggerUI serves the interactive page that reads that document.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();                              // serves /swagger/v1/swagger.json
    app.UseSwaggerUI(options =>                    // serves the UI at /swagger
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Library Management API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
