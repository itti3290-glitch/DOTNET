using Library.Core.Dtos;
using Library.Core.Entities;

namespace Library.Core.Interfaces;

/// <summary>
/// Read methods return untracked data and page in the database.
/// Write methods stage changes; <see cref="IUnitOfWork.SaveChangesAsync"/> commits them.
/// </summary>
public interface IBookRepository
{
    Task<(IReadOnlyList<Book> Items, int Total)> GetPagedAsync(BookQuery query, CancellationToken ct);
    Task<Book?> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>Tracked, for modification.</summary>
    Task<Book?> GetForUpdateAsync(int id, CancellationToken ct);
    Task<bool> IsbnExistsAsync(string isbn, int? excludeId, CancellationToken ct);
    Task AddAsync(Book book, CancellationToken ct);
    void Remove(Book book);
}

public interface IMemberRepository
{
    Task<(IReadOnlyList<Member> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct);
    Task<Member?> GetByIdAsync(int id, CancellationToken ct);
    Task<Member?> GetForUpdateAsync(int id, CancellationToken ct);
    Task<Member?> GetByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(Member member, CancellationToken ct);
    void Remove(Member member);
}

public interface ILoanRepository
{
    Task<(IReadOnlyList<Loan> Items, int Total)> GetPagedAsync(int? memberId, bool activeOnly, int page, int pageSize, CancellationToken ct);
    /// <summary>Untracked, with Book and Member included.</summary>
    Task<Loan?> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>Tracked, with Book and Member included.</summary>
    Task<Loan?> GetForUpdateAsync(int id, CancellationToken ct);
    Task<bool> HasActiveLoanAsync(int bookId, int memberId, CancellationToken ct);
    Task<bool> BookHasLoansAsync(int bookId, CancellationToken ct);
    Task<bool> MemberHasLoansAsync(int memberId, CancellationToken ct);
    Task AddAsync(Loan loan, CancellationToken ct);
}

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Commits staged changes atomically.
    /// Throws <see cref="Exceptions.ConcurrencyConflictException"/> if a row changed since it was read.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
