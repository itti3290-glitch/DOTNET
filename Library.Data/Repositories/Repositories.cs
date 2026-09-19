using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.Data.Repositories;

public class BookRepository(LibraryDbContext db) : IBookRepository
{
    public async Task<(IReadOnlyList<Book> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Books.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(b => b.Categories)
            .OrderBy(b => b.Title).ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
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
