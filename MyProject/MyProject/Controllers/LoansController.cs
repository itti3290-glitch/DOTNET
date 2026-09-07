using Libary.Api.Helpers;
using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Enums;
using LibaryCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Libary.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;

    public LoansController(ILoanService loanService) => _loanService = loanService;

    /// <summary>Borrow a book. The loan is opened for the calling member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Borrow(BorrowBookRequest request, CancellationToken ct)
    {
        var loan = await _loanService.BorrowAsync(request.BookId, User.GetUserId(), ct);
        return CreatedAtAction(nameof(GetMyLoans), null, loan);
    }

    /// <summary>Return a borrowed book. A member returns their own loans; a manager returns anyone's.</summary>
    [HttpPost("{loanId:int}/return")]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Return(int loanId, CancellationToken ct)
        => Ok(await _loanService.ReturnAsync(loanId, User.GetUserId(), User.IsManager(), ct));

    /// <summary>The calling member's own loan history.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(PagedResult<LoanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetMyLoans(
        [FromQuery] LoanQuery query, CancellationToken ct)
        => Ok(await _loanService.GetMyLoansAsync(User.GetUserId(), query, ct));

    /// <summary>Every loan in the system. Manager only.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(typeof(PagedResult<LoanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetAllLoans(
        [FromQuery] LoanQuery query, CancellationToken ct)
        => Ok(await _loanService.GetAllLoansAsync(query, ct));
}
