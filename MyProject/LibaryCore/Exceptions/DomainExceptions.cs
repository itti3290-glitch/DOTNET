namespace LibaryCore.Exceptions;

/// <summary>
/// Base type for every error the domain raises on purpose. The API layer catches
/// <see cref="DomainException"/> in one place and turns each concrete type into the right
/// HTTP status, so services never need to know about HTTP.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>The requested entity does not exist. Maps to 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }
}

/// <summary>
/// The request is well-formed but clashes with the current state - a duplicate ISBN, a book
/// that is already on loan, the member's loan quota is full. Maps to 409.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Input failed a business rule that model validation could not express. Maps to 422.</summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

/// <summary>The caller is authenticated but not allowed to touch this resource. Maps to 403.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Authentication failed - bad credentials or an unusable token. Maps to 401.</summary>
public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message) : base(message) { }
}
