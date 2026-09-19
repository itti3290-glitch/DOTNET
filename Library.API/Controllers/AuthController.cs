using Library.API.Security;
using Library.Core.Dtos;
using Library.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth, IMemberService members, ITokenService tokens) : ControllerBase
{
    /// <summary>Exchanges credentials for a JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var member = await auth.ValidateCredentialsAsync(request, ct);
        return Ok(tokens.CreateToken(member));
    }

    /// <summary>Registers a new Member-role account. Admins cannot be created here.</summary>
    [HttpPost("register")]
    [ProducesResponseType<MemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MemberResponse>> Register(CreateMemberRequest request, CancellationToken ct)
    {
        var created = await members.CreateAsync(request, ct);
        return CreatedAtAction(nameof(MembersController.GetById), "Members", new { id = created.Id }, created);
    }
}
