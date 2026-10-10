using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Data;
using Library.Data.Repositories;
using Library.Service.Services;
using Library.Tests.Services;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Concurrency;

/// <summary>
/// Creates a throw-away PostgreSQL database with the real model (xmin concurrency token + CHECK
/// constraints) and drops it afterwards. These tests need a live server, so an unreachable one
/// is recorded in <see cref="SkipReason"/> and the tests report as skipped rather than failing -
/// or, worse, passing without having proved anything. Point LIBRARY_TEST_CONNECTION at a server
/// to run them.
/// </summary>
public class ConcurrencyDbFixture : IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LIBRARY_TEST_CONNECTION")
        ?? $"Host=localhost;Port=5432;Database=LibraryTests_{Guid.NewGuid():N};Username=postgres;Password=postgres";

    /// <summary>Null when the database is usable; otherwise why these tests cannot run.</summary>
    public string? SkipReason { get; private set; }

    public LibraryDbContext NewContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var db = NewContext();
            await db.Database.EnsureCreatedAsync(); // model incl. seed members 1 and 2
        }
        catch (Exception ex)
        {
            SkipReason = $"No PostgreSQL server reachable for the concurrency tests. " +
                         $"Set LIBRARY_TEST_CONNECTION to run them. ({ex.GetType().Name}: {ex.Message})";
        }
    }

    public async Task DisposeAsync()
    {
        if (SkipReason is not null) return;

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
    [SkippableFact]
    public async Task TwoContextsChangingTheSameBook_FirstSaveWins_SecondThrowsDbUpdateConcurrencyException()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

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

    [SkippableFact]
    public async Task UnitOfWork_TranslatesTheEfExceptionIntoConcurrencyConflictException()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

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

    [SkippableFact]
    public async Task TwoMembersRacingForTheLastCopy_ExactlyOneGetsIt()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

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
        // Depending on timing the loser either hit the xmin concurrency check or saw zero copies left.
        Assert.True(loser.Error is ConcurrencyConflictException or NoCopiesAvailableException,
            $"Unexpected failure: {loser.Error}");

        await using var check = fixture.NewContext();
        var book = await check.Books.AsNoTracking().SingleAsync(b => b.Id == bookId);
        Assert.Equal(0, book.AvailableCopies);
        Assert.Equal(1, await check.Loans.CountAsync(l => l.BookId == bookId)); // never two loans for one copy
    }

    [SkippableFact]
    public async Task Database_RejectsNegativeStock_EvenWhenTheDomainMethodIsBypassed()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var bookId = await fixture.AddBookAsync(copies: 1);
        await using var db = fixture.NewContext();
        var book = await db.Books.SingleAsync(b => b.Id == bookId);

        book.AvailableCopies = -1; // bypasses the domain method on purpose

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
