using AutoMapper;
using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Core.Interfaces;

namespace Library.Service.Services;

public class LoanService(
    ILoanRepository loans,
    IBookRepository books,
    IMemberRepository members,
    IUnitOfWork uow,
    IMapper mapper) : ILoanService
{
    public async Task<PagedResult<LoanResponse>> GetAllAsync(int? memberId, bool activeOnly, PageQuery query, CancellationToken ct)
    {
        var (items, total) = await loans.GetPagedAsync(memberId, activeOnly, query.SafePage, query.SafePageSize, ct);
        return new PagedResult<LoanResponse>(mapper.Map<List<LoanResponse>>(items), query.SafePage, query.SafePageSize, total);
    }

    public async Task<LoanResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Loan), id);
        return mapper.Map<LoanResponse>(loan);
    }

    public async Task<LoanResponse> LoanBookAsync(CreateLoanRequest request, CancellationToken ct)
    {
        // Tracked read: the Book's xmin concurrency token is captured here and checked again at SaveChanges.
        var book = await books.GetForUpdateAsync(request.BookId, ct)
            ?? throw new NotFoundException(nameof(Book), request.BookId);
        if (await members.GetByIdAsync(request.MemberId, ct) is null)
            throw new NotFoundException(nameof(Member), request.MemberId);
        if (await loans.HasActiveLoanAsync(request.BookId, request.MemberId, ct))
            throw new BusinessRuleException("This member already has an active loan of this book.");

        book.TakeCopy(); // Throws NoCopiesAvailableException at zero.

        var loan = new Loan { BookId = book.Id, MemberId = request.MemberId, LoanedAt = DateTime.UtcNow };
        await loans.AddAsync(loan, ct);

        // If another request changed this Book since we read it, this throws ConcurrencyConflictException
        // and neither the decrement nor the loan is saved.
        await uow.SaveChangesAsync(ct);

        return await GetByIdAsync(loan.Id, ct);
    }

    public async Task<LoanResponse> ReturnBookAsync(int loanId, CancellationToken ct)
    {
        var loan = await loans.GetForUpdateAsync(loanId, ct) ?? throw new NotFoundException(nameof(Loan), loanId);
        if (loan.ReturnedAt is not null)
            throw new BusinessRuleException("This loan has already been returned.");

        loan.Book.ReturnCopy();
        loan.ReturnedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync(ct); // ConcurrencyConflictException if the Book row changed meanwhile.

        return mapper.Map<LoanResponse>(loan);
    }
}
