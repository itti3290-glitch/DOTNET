using System.Security.Claims;

namespace Library.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token has no valid user id.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
}
