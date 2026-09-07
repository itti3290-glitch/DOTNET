using System.Text.Json;
using LibaryCore.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Libary.Api.Middlewares;

/// <summary>
/// One place where every unhandled exception becomes an HTTP response. Domain exceptions map to
/// their intended status code; anything else is a 500 with no internals leaked to the client.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Request conflicts with current state"),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Validation failed"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            AuthenticationException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogInformation("Request failed ({Status}): {Message}", status, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? "See server logs for details."
                : ex.Message,
            Instance = context.Request.Path
        };

        if (ex is ValidationException { Errors.Count: > 0 } validation)
            problem.Extensions["errors"] = validation.Errors;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
