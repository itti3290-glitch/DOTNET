using Library.Core.Dtos;
using Library.Core.Enums;
using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.Data.Repositories;

public class BookRepository(LibraryDbContext db) : IBookRepository
{
    public async Task<(IReadOnlyList<Book> Items, int Total)> GetPagedAsync(BookQuery q, CancellationToken ct)
    {
        var query = db.Books.AsNoTracking();

        // Filters: all translate to SQL WHERE clauses (Contains -> LIKE, wildcards are escaped by EF).
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(b => b.Title.Contains(s) || b.Author.Contains(s) || b.Isbn.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(q.Author))
        {
            var a = q.Author.Trim();
            query = query.Where(b => b.Author.Contains(a));
        }
        if (q.CategoryId is not null)
            query = query.Where(b => b.Categories.Any(c => c.Id == q.CategoryId));
        if (q.AvailableOnly)
            query = query.Where(b => b.AvailableCopies > 0);

        var total = await query.CountAsync(ct);

        // Sorting: whitelisted by enum; Id as a tie-breaker keeps paging stable.
        var ordered = q.SortBy switch
        {
            BookSortBy.Author => q.Desc ? query.OrderByDescending(b => b.Author) : query.OrderBy(b => b.Author),
            BookSortBy.Availability => q.Desc ? query.OrderByDescending(b => b.AvailableCopies) : query.OrderBy(b => b.AvailableCopies),
            _ => q.Desc ? query.OrderByDescending(b => b.Title) : query.OrderBy(b => b.Title)
        };

        var items = await ordered
            .ThenBy(b => b.Id)
            .Include(b => b.Categories)
            .Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Book?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Books.AsNoTracking().Include(b => b.Categories).FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Book?> GetForUpdateAsync(int id, CancellationToken ct) =>
        db.Books.Include(b => b.Categories).FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<bool> IsbnExistsAsync(string isbn, int? excludeId, CancellationToken ct) =>
        db.Books.AsNoTracking().AnyAsync(b => b.Isbn == isbn && b.Id != excludeId, ct);

    public async Task AddAsync(Book book, CancellationToken ct) => await db.Books.AddAsync(book, ct);

    public void Remove(Book book) => db.Books.Remove(book);
}

public class MemberRepository(LibraryDbContext db) : IMemberRepository
{
    public async Task<(IReadOnlyList<Member> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Members.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.FullName).ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Member?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Member?> GetForUpdateAsync(int id, CancellationToken ct) =>
        db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Member?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == email, ct);

    public async Task AddAsync(Member member, CancellationToken ct) => await db.Members.AddAsync(member, ct);

    public void Remove(Member member) => db.Members.Remove(member);
}

public class LoanRepository(LibraryDbContext db) : ILoanRepository
{
    public async Task<(IReadOnlyList<Loan> Items, int Total)> GetPagedAsync(
        int? memberId, bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Loans.AsNoTracking();
        if (memberId is not null) query = query.Where(l => l.MemberId == memberId);
        if (activeOnly) query = query.Where(l => l.ReturnedAt == null);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(l => l.Book).Include(l => l.Member)
            .OrderByDescending(l => l.LoanedAt).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Loan?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Loans.AsNoTracking().Include(l => l.Book).Include(l => l.Member).FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<Loan?> GetForUpdateAsync(int id, CancellationToken ct) =>
        db.Loans.Include(l => l.Book).Include(l => l.Member).FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<bool> HasActiveLoanAsync(int bookId, int memberId, CancellationToken ct) =>
        db.Loans.AsNoTracking().AnyAsync(l => l.BookId == bookId && l.MemberId == memberId && l.ReturnedAt == null, ct);

    public Task<bool> BookHasLoansAsync(int bookId, CancellationToken ct) =>
        db.Loans.AsNoTracking().AnyAsync(l => l.BookId == bookId, ct);

    public Task<bool> MemberHasLoansAsync(int memberId, CancellationToken ct) =>
        db.Loans.AsNoTracking().AnyAsync(l => l.MemberId == memberId, ct);

    public async Task AddAsync(Loan loan, CancellationToken ct) => await db.Loans.AddAsync(loan, ct);
}

public class CategoryRepository(LibraryDbContext db) : ICategoryRepository
{
    // Tracked on purpose: these are attached to a Book being created/updated.
    public async Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Categories.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
}

public class UnitOfWork(LibraryDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate so Service/API never need to know about EF Core.
            throw new ConcurrencyConflictException(
                "The resource was modified by another request. Reload it and try again.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two requests passed the same "does it exist?" check and both inserted (e.g. the same
            // member borrowing the same book twice at once). The unique index let only one through.
            throw new ConcurrencyConflictException(
                "A conflicting record was created by another request. Reload and try again.", ex);
        }
    }

    // SQL Server: 2601 = duplicate key in a unique index, 2627 = unique constraint violation.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
