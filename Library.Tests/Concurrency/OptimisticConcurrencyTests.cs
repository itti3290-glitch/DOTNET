using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Data;
using Library.Data.Repositories;
using Library.Service.Services;
using Library.Tests.Services;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Concurrency;

/// <summary>Creates a throw-away LocalDB database with the real model (rowversion + CHECK constraints) and drops it afterwards.</summary>
public class ConcurrencyDbFixture : IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LIBRARY_TEST_CONNECTION")
        ?? $"Server=(localdb)\\MSSQLLocalDB;Database=LibraryTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    public LibraryDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync(); // model incl. seed members 1 and 2
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>Adds a fresh book so each test is independent of the others.</summary>
    public async Task<int> AddBookAsync(int copies)
    {
        await using var db = NewContext();
        var book = new Book
        {
            Title = "Test book", Author = "Test author",
            Isbn = Guid.NewGuid().ToString("N")[..13],
            TotalCopies = copies, AvailableCopies = copies
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book.Id;
    }
}

/// <summary>
/// Proof that the optimistic concurrency token works against a real database:
/// two contexts read the same book, both change it, the first save wins and the second is rejected.
/// </summary>
public class OptimisticConcurrencyTests(ConcurrencyDbFixture fixture) : IClassFixture<ConcurrencyDbFixture>
{
    [Fact]
    public async Task TwoContextsChangingTheSameBook_FirstSaveWins_SecondThrowsDbUpdateConcurrencyException()
    {
        var bookId = await fixture.AddBookAsync(copies: 1);
        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();

        // Both users read the book while one copy is still free.
        var bookInFirst = await first.Books.SingleAsync(b => b.Id == bookId);
        var bookInSecond = await second.Books.SingleAsync(b => b.Id == bookId);
        Assert.Equal(1, bookInFirst.AvailableCopies);
        Assert.Equal(1, bookInSecond.AvailableCopies);

        // Both take the last copy.
        bookInFirst.TakeCopy();
        bookInSecond.TakeCopy();

        await first.SaveChangesAsync();                                                   // first one wins
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync()); // second is rejected

        await using var check = fixture.NewContext();
        var stored = await check.Books.AsNoTracking().SingleAsync(b => b.Id == bookId);
        Assert.Equal(0, stored.AvailableCopies); // decremented once, not twice
    }

    [Fact]
    public async Task UnitOfWork_TranslatesTheEfExceptionIntoConcurrencyConflictException()
    {
        var bookId = await fixture.AddBookAsync(copies: 1);
        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();

        var bookInFirst = await first.Books.SingleAsync(b => b.Id == bookId);
        var bookInSecond = await second.Books.SingleAsync(b => b.Id == bookId);
        bookInFirst.TakeCopy();
        bookInSecond.TakeCopy();

        await new UnitOfWork(first).SaveChangesAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => new UnitOfWork(second).SaveChangesAsync(CancellationToken.None));
        Assert.IsType<DbUpdateConcurrencyException>(ex.InnerException);
    }

    [Fact]
    public async Task TwoMembersRacingForTheLastCopy_ExactlyOneGetsIt()
    {
        var bookId = await fixture.AddBookAsync(copies: 1);

        // Two separate "requests": each has its own DbContext and its own service instances.
        Task<LoanResponse> Borrow(int memberId)
        {
            var db = fixture.NewContext();
            var service = new LoanService(
                new LoanRepository(db), new BookRepository(db), new MemberRepository(db),
                new UnitOfWork(db), TestHelpers.CreateMapper());
            return service.LoanBookAsync(new CreateLoanRequest(bookId, memberId), CancellationToken.None);
        }

        var outcomes = await Task.WhenAll(
            Task.Run(async () => { try { return (Ok: true, Error: (Exception?)null, Result: await Borrow(1)); } catch (Exception e) { return (false, e, null!); } }),
            Task.Run(async () => { try { return (Ok: true, Error: (Exception?)null, Result: await Borrow(2)); } catch (Exception e) { return (false, e, null!); } }));

        Assert.Equal(1, outcomes.Count(o => o.Ok));
        var loser = Assert.Single(outcomes, o => !o.Ok);
        // Depending on timing the loser either hit the row-version check or saw zero copies left.
        Assert.True(loser.Error is ConcurrencyConflictException or NoCopiesAvailableException,
            $"Unexpected failure: {loser.Error}");

        await using var check = fixture.NewContext();
        var book = await check.Books.AsNoTracking().SingleAsync(b => b.Id == bookId);
        Assert.Equal(0, book.AvailableCopies);
        Assert.Equal(1, await check.Loans.CountAsync(l => l.BookId == bookId)); // never two loans for one copy
    }

    [Fact]
    public async Task Database_RejectsNegativeStock_EvenWhenTheDomainMethodIsBypassed()
    {
        var bookId = await fixture.AddBookAsync(copies: 1);
        await using var db = fixture.NewContext();
        var book = await db.Books.SingleAsync(b => b.Id == bookId);

        book.AvailableCopies = -1; // bypasses the domain method on purpose

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
