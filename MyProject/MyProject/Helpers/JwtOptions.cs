using System.ComponentModel.DataAnnotations;

namespace Libary.Api.Helpers;

/// <summary>Bound from the <c>Jwt</c> configuration section. All fields are required.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = default!;
    [Required] public string Audience { get; set; } = default!;

    /// <summary>HS256 signing secret. Must be at least 32 characters (256 bits).</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = default!;

    [Range(1, 43200)]
    public int AccessTokenMinutes { get; set; } = 120;
}
