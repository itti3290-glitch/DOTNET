namespace Library.API.Middlewares;

/// <summary>
/// Creates a unique CorrelationId per request and makes it available in
/// HttpContext.Items, the response header, and the logging scope (so NLog prints it on every line).
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        // Always generated server-side: a client-supplied value could be used to forge log entries.
        var correlationId = Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }
}

public static class HttpContextCorrelationExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[CorrelationIdMiddleware.ItemKey] as string ?? context.TraceIdentifier;
}
