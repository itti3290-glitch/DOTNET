using Microsoft.AspNetCore.Mvc;

namespace Library.API.Middlewares;

public static class ValidationResponseExtensions
{
    /// <summary>
    /// Makes model-validation failures (Data Annotations, malformed JSON) return the same JSON shape as
    /// every other error, plus a per-field "errors" map, instead of the framework's ProblemDetails.
    /// </summary>
    public static IServiceCollection AddUniformValidationResponse(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(o =>
        {
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var failed = ctx.ModelState.Where(e => e.Value is { Errors.Count: > 0 }).ToList();

                // Keys starting with "$" come from the JSON parser (malformed body / wrong type). Their messages
                // contain internal type names and byte offsets, so replace them with one clean message.
                Dictionary<string, string[]> errors = failed.Any(e => e.Key.StartsWith('$'))
                    ? new() { ["body"] = ["The request body is missing, is not valid JSON, or has a value of the wrong type."] }
                    : failed.ToDictionary(
                        e => string.IsNullOrEmpty(e.Key) ? "request" : ToCamelCase(e.Key),
                        e => e.Value!.Errors
                            .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "The value is invalid." : x.ErrorMessage)
                            .ToArray());

                return new BadRequestObjectResult(new
                {
                    status = StatusCodes.Status400BadRequest,
                    error = "Bad Request",
                    message = "One or more fields are invalid.",
                    errors,
                    correlationId = ctx.HttpContext.GetCorrelationId()
                });
            };
        });

    private static string ToCamelCase(string key) =>
        key.Length == 0 || char.IsLower(key[0]) || key[0] == '$' ? key : char.ToLowerInvariant(key[0]) + key[1..];
}
