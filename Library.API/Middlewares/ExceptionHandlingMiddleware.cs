using System.Text.Json;
using Library.Core.Exceptions;

namespace Library.API.Middlewares;

/// <summary>Maps exceptions to a uniform JSON error payload.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nobody is listening for a response.
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Bad Request"),
            // Out of stock is a state conflict, not a malformed request, so it is a 409 rather than a 400.
            NoCopiesAvailableException => (StatusCodes.Status409Conflict, "Conflict"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Bad Request"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else if (ex is ConcurrencyConflictException or NoCopiesAvailableException)
            logger.LogWarning(ex, "Resource conflict on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("{Type} on {Method} {Path}: {Message}", ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

        if (context.Response.HasStarted) return; // Too late to change the response.

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status,
            error = title,
            // Never leak internals for unexpected errors.
            message = status == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : ex.Message,
            correlationId = context.GetCorrelationId()
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}
