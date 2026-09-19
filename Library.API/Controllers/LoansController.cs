using Library.API.Security;
using Library.Core.Dtos;
using Library.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController(ILoanService loans) : ControllerBase
{
    /// <summary>Admins see any loan; members only their own.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> GetById(int id, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(id, ct);
        if (!User.IsAdmin() && loan.MemberId != User.GetUserId()) return Forbid();
        return Ok(loan);
    }

    /// <summary>Active loans: all of them for admins, own for members.</summary>
    [HttpGet("active")]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetActive([FromQuery] PageQuery query, CancellationToken ct)
    {
        int? memberId = User.IsAdmin() ? null : User.GetUserId();
        return Ok(await loans.GetAllAsync(memberId, activeOnly: true, query, ct));
    }

    /// <summary>Creates a loan (borrows one copy). Members can borrow only for themselves.</summary>
    [HttpPost]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Create(CreateLoanRequest request, CancellationToken ct)
    {
        if (!User.IsAdmin() && request.MemberId != User.GetUserId()) return Forbid();

        var loan = await loans.LoanBookAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = loan.Id }, loan);
    }

    /// <summary>Returns the loan's copy to the library. Members can return only their own loans.</summary>
    [HttpPost("{id:int}/return")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Return(int id, CancellationToken ct)
    {
        if (!User.IsAdmin())
        {
            var existing = await loans.GetByIdAsync(id, ct);
            if (existing.MemberId != User.GetUserId()) return Forbid();
        }

        return Ok(await loans.ReturnBookAsync(id, ct));
    }
}
