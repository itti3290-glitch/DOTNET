using LibaryCore.Entities;
using LibaryCore.Enums;
using LibaryData.Context;
using Microsoft.EntityFrameworkCore;

namespace LibaryData.Repositories;

/// <summary>Wraps the one <see cref="LibaryDbContext"/> instance of the current request scope.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly LibaryDbContext _context;

    public UnitOfWork(LibaryDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public void DiscardChanges() => _context.ChangeTracker.Clear();
}

public sealed class UserRepository : IUserRepository
{
    private readonly LibaryDbContext _context;

    public UserRepository(LibaryDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUserNameOrEmailAsync(string value, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.UserName == value || u.Email == value, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);
}

public sealed class BookRepository : IBookRepository
{
    private readonly LibaryDbContext _context;

    public BookRepository(LibaryDbContext context) => _context = context;

    public Task<Book?> GetByIdAsync(int id, CancellationToken ct = default)
        => _context.Books.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<bool> IsbnExistsAsync(string isbn, int? excludingBookId = null, CancellationToken ct = default)
        => _context.Books.AnyAsync(
            b => b.Isbn == isbn && (excludingBookId == null || b.Id != excludingBookId), ct);

    public async Task<(IReadOnlyList<Book> Items, int TotalCount)> SearchAsync(
        string? search, BookStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Book> q = _context.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(b => EF.Functions.Like(b.Title, $"%{term}%")
                          || EF.Functions.Like(b.Author, $"%{term}%"));
        }

        if (status is not null)
            q = q.Where(b => b.Status == status);

        var total = await q.CountAsync(ct);

        var items = await q.OrderBy(b => b.Title)
                           .ThenBy(b => b.Id)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Book book, CancellationToken ct = default)
        => await _context.Books.AddAsync(book, ct);

    public void Remove(Book book) => _context.Books.Remove(book);
}

public sealed class LoanRepository : ILoanRepository
{
    private readonly LibaryDbContext _context;

    public LoanRepository(LibaryDbContext context) => _context = context;

    public Task<Loan?> GetByIdAsync(int id, bool includeBookAndMember = false, CancellationToken ct = default)
    {
        IQueryable<Loan> q = _context.Loans;
        if (includeBookAndMember)
            q = q.Include(l => l.Book).Include(l => l.Member);
        return q.FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public Task<int> CountActiveByMemberAsync(int memberId, CancellationToken ct = default)
        => _context.Loans.CountAsync(
            l => l.MemberId == memberId && l.Status == LoanStatus.Active, ct);

    public Task<bool> HasActiveLoanForBookAsync(int bookId, int memberId, CancellationToken ct = default)
        => _context.Loans.AnyAsync(
            l => l.BookId == bookId && l.MemberId == memberId && l.Status == LoanStatus.Active, ct);

    public async Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(
        int? memberId, LoanStatus? status, bool overdueOnly, DateTime utcNow,
        int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Loan> q = _context.Loans
            .AsNoTracking()
            .Include(l => l.Book)
            .Include(l => l.Member);

        if (memberId is not null)
            q = q.Where(l => l.MemberId == memberId);

        if (status is not null)
            q = q.Where(l => l.Status == status);

        if (overdueOnly)
            q = q.Where(l => l.Status == LoanStatus.Active && l.DueAtUtc < utcNow);

        var total = await q.CountAsync(ct);

        var items = await q.OrderByDescending(l => l.BorrowedAtUtc)
                           .ThenByDescending(l => l.Id)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Loan loan, CancellationToken ct = default)
        => await _context.Loans.AddAsync(loan, ct);
}
