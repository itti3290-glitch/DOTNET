namespace Library.Core.Exceptions;

/// <summary>Expected, business-level failure. Maps to HTTP 409 unless a subtype says otherwise.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>Maps to HTTP 404.</summary>
public class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

/// <summary>No copies left to loan. Maps to HTTP 409.</summary>
public class NoCopiesAvailableException(int bookId)
    : BusinessRuleException($"No copies of book {bookId} are currently available.");

/// <summary>
/// Another request changed the same row first (optimistic concurrency). Maps to HTTP 409.
/// Raised by the data layer so upper layers never depend on EF Core.
/// </summary>
public class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Invalid input. Maps to HTTP 400.</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>Bad credentials. Maps to HTTP 401.</summary>
public class AuthenticationFailedException() : Exception("Invalid email or password.");
