using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LibaryCore.Enums;

namespace Libary.Api.Helpers;

/// <summary>Reads the strongly-typed identity out of the validated bearer token.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id, or throws if the principal carries no usable id claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException("The token does not contain a valid user id.");
    }

    public static bool IsManager(this ClaimsPrincipal principal)
        => principal.IsInRole(nameof(UserRole.Manager));
}
