using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Entities;
using LibaryCore.Enums;
using LibaryCore.Exceptions;
using LibaryCore.Services;
using LibaryData.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LibaryService.Services;

/// <inheritdoc cref="ILoanService"/>
public sealed class LoanService : ILoanService
{
    /// <summary>How many times a borrow attempt is replayed after losing the race for the copy.</summary>
    private const int MaxBorrowAttempts = 3;

    private readonly IBookRepository _books;
    private readonly ILoanRepository _loans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibaryOptions _options;

    public LoanService(
        IBookRepository books,
        ILoanRepository loans,
        IUnitOfWork unitOfWork,
        LibaryOptions options)
    {
        _books = books;
        _loans = loans;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    public async Task<LoanResponse> BorrowAsync(int bookId, int memberId, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var book = await _books.GetByIdAsync(bookId, ct)
                ?? throw new NotFoundException("Book", bookId);

            switch (book.Status)
            {
                case BookStatus.Maintenance:
                    throw new ConflictException("This book is withdrawn from circulation.");
                case BookStatus.Loaned:
                    throw new ConflictException("This book is currently on loan.");
            }

            if (await _loans.HasActiveLoanForBookAsync(bookId, memberId, ct))
                throw new ConflictException("You already have this book on loan.");

            var activeCount = await _loans.CountActiveByMemberAsync(memberId, ct);
            if (activeCount >= _options.MaxActiveLoansPerMember)
                throw new ConflictException(
                    $"You already hold the maximum of {_options.MaxActiveLoansPerMember} books.");

            var now = DateTime.UtcNow;
            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = memberId,
                BorrowedAtUtc = now,
                DueAtUtc = now.AddDays(_options.LoanPeriodDays),
                Status = LoanStatus.Active
            };

            // Flip the copy to Loaned in the same transaction. Book.RowVersion is the concurrency
            // token: if another member won the race between our read and our save, this update
            // matches zero rows and EF raises DbUpdateConcurrencyException.
            book.Status = BookStatus.Loaned;
            await _loans.AddAsync(loan, ct);

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxBorrowAttempts)
            {
                _unitOfWork.DiscardChanges();
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "Another member borrowed this book a moment ago. Please try again.");
            }

            return await LoadResponseAsync(loan.Id, now, ct);
        }
    }

    public async Task<LoanResponse> ReturnAsync(
        int loanId, int actingUserId, bool actingUserIsManager, CancellationToken ct = default)
    {
        try
        {
            var loan = await _loans.GetByIdAsync(loanId, includeBookAndMember: true, ct)
                ?? throw new NotFoundException("Loan", loanId);

            if (loan.MemberId != actingUserId && !actingUserIsManager)
                throw new ForbiddenException("You can only return your own loans.");

            if (loan.Status == LoanStatus.Returned || loan.ReturnedAtUtc is not null)
                throw new ConflictException("This loan has already been closed.");

            var now = DateTime.UtcNow;
            loan.ReturnedAtUtc = now;
            loan.Status = LoanStatus.Returned;

            // Only hand the copy back to the shelf if it is still marked as out - a manager may
            // have parked it in maintenance in the meantime.
            if (loan.Book.Status == BookStatus.Loaned)
                loan.Book.Status = BookStatus.Available;

            await _unitOfWork.SaveChangesAsync(ct);

            return loan.ToResponse(now);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This loan was just updated elsewhere. Please retry.");
        }
    }

    public async Task<PagedResult<LoanResponse>> GetMyLoansAsync(
        int memberId, LoanQuery query, CancellationToken ct = default)
        => await SearchAsync(memberId, query, ct);

    public async Task<PagedResult<LoanResponse>> GetAllLoansAsync(
        LoanQuery query, CancellationToken ct = default)
        => await SearchAsync(memberId: null, query, ct);

    private async Task<PagedResult<LoanResponse>> SearchAsync(
        int? memberId, LoanQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var (items, total) = await _loans.SearchAsync(
            memberId, query.Status, query.OverdueOnly, now, query.Page, query.PageSize, ct);

        var mapped = items.Select(l => l.ToResponse(now)).ToList();
        return PagedResult<LoanResponse>.Create(mapped, query.Page, query.PageSize, total);
    }

    private async Task<LoanResponse> LoadResponseAsync(int loanId, DateTime now, CancellationToken ct)
    {
        var loan = await _loans.GetByIdAsync(loanId, includeBookAndMember: true, ct)
            ?? throw new NotFoundException("Loan", loanId);

        return loan.ToResponse(now);
    }
}
