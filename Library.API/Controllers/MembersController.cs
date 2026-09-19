using Library.API.Security;
using Library.Core.Dtos;
using Library.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController(IMemberService members) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<MemberResponse>>> GetAll([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await members.GetAllAsync(query, ct));

    /// <summary>Admins can read any member; members can read only themselves.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberResponse>> GetById(int id, CancellationToken ct)
    {
        if (!User.IsAdmin() && User.GetUserId() != id) return Forbid();
        return Ok(await members.GetByIdAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(int id, UpdateMemberRequest request, CancellationToken ct)
    {
        await members.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await members.DeleteAsync(id, ct);
        return NoContent();
    }
}
