using System.Diagnostics;

namespace Library.API.Middlewares;

/// <summary>
/// One log line per request with method, path, status and duration.
/// Deliberately logs no headers, query string or body, so passwords and tokens never reach the log.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path);

        await next(context);

        logger.LogInformation("Request finished {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
