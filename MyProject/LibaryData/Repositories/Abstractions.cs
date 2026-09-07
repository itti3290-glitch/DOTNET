using LibaryCore.Entities;
using LibaryCore.Enums;

namespace LibaryData.Repositories;

/// <summary>
/// Commits everything the repositories have staged in the current scope as one transaction.
/// Kept separate from the repositories so a service can touch several of them and still save once.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Drop every tracked change and start clean. Used by the borrow flow when an optimistic
    /// concurrency clash means the whole attempt has to be replayed against fresh data.
    /// </summary>
    void DiscardChanges();
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUserNameOrEmailAsync(string value, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> IsbnExistsAsync(string isbn, int? excludingBookId = null, CancellationToken ct = default);

    /// <summary>One materialised page of the catalog plus the unpaged total.</summary>
    Task<(IReadOnlyList<Book> Items, int TotalCount)> SearchAsync(
        string? search, BookStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task AddAsync(Book book, CancellationToken ct = default);
    void Remove(Book book);
}

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(int id, bool includeBookAndMember = false, CancellationToken ct = default);
    Task<int> CountActiveByMemberAsync(int memberId, CancellationToken ct = default);
    Task<bool> HasActiveLoanForBookAsync(int bookId, int memberId, CancellationToken ct = default);

    /// <summary>One materialised page of loans (newest first) plus the unpaged total.</summary>
    Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(
        int? memberId, LoanStatus? status, bool overdueOnly, DateTime utcNow,
        int page, int pageSize, CancellationToken ct = default);

    Task AddAsync(Loan loan, CancellationToken ct = default);
}
