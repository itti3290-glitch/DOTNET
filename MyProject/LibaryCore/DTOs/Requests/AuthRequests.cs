using System.ComponentModel.DataAnnotations;

namespace LibaryCore.DTOs.Requests;

/// <summary>Payload for <c>POST /api/auth/register</c>. Always creates a Borrower account.</summary>
public record RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; init; } = default!;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; init; } = default!;

    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; init; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = default!;
}

/// <summary>Payload for <c>POST /api/auth/login</c>. Accepts either the username or the email.</summary>
public record LoginRequest
{
    [Required]
    public string UserNameOrEmail { get; init; } = default!;

    [Required]
    public string Password { get; init; } = default!;
}
