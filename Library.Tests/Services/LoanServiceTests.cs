using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Enums;
using Library.Core.Exceptions;
using Library.Core.Interfaces;
using Library.Service.Services;
using Moq;

namespace Library.Tests.Services;

/// <summary>
/// Unit tests for the limited-resource logic in LoanService. Repositories and the unit of work are mocked,
/// so these run without a database.
/// </summary>
public class LoanServiceTests
{
    // A real, non-default token: lets us prove the token is passed down instead of being dropped.
    private readonly CancellationToken _ct = new CancellationTokenSource().Token;

    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IBookRepository> _books = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private LoanService CreateService() =>
        new(_loans.Object, _books.Object, _members.Object, _uow.Object, TestHelpers.CreateMapper());

    private static Book NewBook(int available, int total = 3) =>
        new() { Id = 3, Title = "Dune", Author = "Frank Herbert", Isbn = "9780441013593", TotalCopies = total, AvailableCopies = available };

    private static Member NewMember() => new() { Id = 1, FullName = "Library Admin", Email = "admin@library.local" };

    /// <summary>Wires the mocks for a normal borrow: book and member exist, no open loan yet.</summary>
    private void SetupBorrowHappyPath(Book book, Member member)
    {
        _books.Setup(b => b.GetForUpdateAsync(book.Id, _ct)).ReturnsAsync(book);
        _members.Setup(m => m.GetByIdAsync(member.Id, _ct)).ReturnsAsync(member);
        _loans.Setup(l => l.HasActiveLoanAsync(book.Id, member.Id, _ct)).ReturnsAsync(false);
    }

    // ---------------------------------------------------------------- borrow: accepted

    [Fact]
    public async Task LoanBook_WhenCopyAvailable_TakesCopySavesLoanAndReturnsIt()
    {
        var book = NewBook(available: 1);
        var member = NewMember();
        SetupBorrowHappyPath(book, member);

        Loan? added = null;
        _loans.Setup(l => l.AddAsync(It.IsAny<Loan>(), _ct))
            .Callback<Loan, CancellationToken>((l, _) => { l.Id = 42; added = l; })
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(_ct)).ReturnsAsync(2);
        _loans.Setup(l => l.GetByIdAsync(42, _ct))
            .ReturnsAsync(() => new Loan { Id = 42, BookId = 3, Book = book, MemberId = 1, Member = member, LoanedAt = added!.LoanedAt });

        var result = await CreateService().LoanBookAsync(new CreateLoanRequest(3, 1), _ct);

        Assert.Equal(0, book.AvailableCopies);                       // the copy was taken
        Assert.NotNull(added);
        Assert.Equal(3, added!.BookId);
        Assert.Equal(1, added.MemberId);
        Assert.Null(added.ReturnedAt);                               // loan is open
        _loans.Verify(l => l.AddAsync(It.IsAny<Loan>(), _ct), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(_ct), Times.Once);       // committed exactly once, with our token
        Assert.Equal(42, result.Id);
        Assert.Equal("Dune", result.BookTitle);
        Assert.Equal(LoanStatus.Active, result.Status);
    }

    // ---------------------------------------------------------------- borrow: rejected

    [Fact]
    public async Task LoanBook_WhenNoCopiesLeft_IsRejectedAndNothingIsSaved()
    {
        var book = NewBook(available: 0);
        SetupBorrowHappyPath(book, NewMember());

        await Assert.ThrowsAsync<NoCopiesAvailableException>(
            () => CreateService().LoanBookAsync(new CreateLoanRequest(3, 1), _ct));

        Assert.Equal(0, book.AvailableCopies);                       // never goes negative
        _loans.Verify(l => l.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoanBook_WhenAnotherRequestTookTheCopyFirst_ThrowsConcurrencyConflict()
    {
        // The service read the book with a copy available, but by the time it saves, someone else
        // has changed the row: the data layer reports it as ConcurrencyConflictException.
        var book = NewBook(available: 1);
        SetupBorrowHappyPath(book, NewMember());
        _uow.Setup(u => u.SaveChangesAsync(_ct))
            .ThrowsAsync(new ConcurrencyConflictException("The resource was modified by another request."));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => CreateService().LoanBookAsync(new CreateLoanRequest(3, 1), _ct));

        _uow.Verify(u => u.SaveChangesAsync(_ct), Times.Once);
        // No response is built for a failed loan.
        _loans.Verify(l => l.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoanBook_WhenMemberAlreadyHoldsTheBook_IsRejectedBeforeTakingACopy()
    {
        var book = NewBook(available: 2);
        var member = NewMember();
        SetupBorrowHappyPath(book, member);
        _loans.Setup(l => l.HasActiveLoanAsync(3, 1, _ct)).ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().LoanBookAsync(new CreateLoanRequest(3, 1), _ct));

        Assert.Equal(2, book.AvailableCopies);                       // untouched
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoanBook_WhenBookDoesNotExist_ThrowsNotFound()
    {
        _books.Setup(b => b.GetForUpdateAsync(99, _ct)).ReturnsAsync((Book?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().LoanBookAsync(new CreateLoanRequest(99, 1), _ct));
    }

    [Fact]
    public async Task LoanBook_WhenMemberDoesNotExist_ThrowsNotFound()
    {
        _books.Setup(b => b.GetForUpdateAsync(3, _ct)).ReturnsAsync(NewBook(available: 1));
        _members.Setup(m => m.GetByIdAsync(99, _ct)).ReturnsAsync((Member?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().LoanBookAsync(new CreateLoanRequest(3, 99), _ct));
    }

    // ---------------------------------------------------------------- return

    [Fact]
    public async Task ReturnBook_GivesTheCopyBackAndClosesTheLoan()
    {
        var book = NewBook(available: 0, total: 1);
        var loan = new Loan { Id = 7, BookId = 3, Book = book, MemberId = 1, Member = NewMember(), LoanedAt = DateTime.UtcNow.AddDays(-2) };
        _loans.Setup(l => l.GetForUpdateAsync(7, _ct)).ReturnsAsync(loan);
        _uow.Setup(u => u.SaveChangesAsync(_ct)).ReturnsAsync(2);

        var result = await CreateService().ReturnBookAsync(7, _ct);

        Assert.Equal(1, book.AvailableCopies);
        Assert.NotNull(loan.ReturnedAt);
        Assert.Equal(LoanStatus.Returned, result.Status);
        _uow.Verify(u => u.SaveChangesAsync(_ct), Times.Once);
    }

    [Fact]
    public async Task ReturnBook_WhenAlreadyReturned_IsRejectedAndCopyCountIsNotInflated()
    {
        var book = NewBook(available: 1, total: 1);
        var loan = new Loan { Id = 7, BookId = 3, Book = book, MemberId = 1, Member = NewMember(), ReturnedAt = DateTime.UtcNow };
        _loans.Setup(l => l.GetForUpdateAsync(7, _ct)).ReturnsAsync(loan);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ReturnBookAsync(7, _ct));

        Assert.Equal(1, book.AvailableCopies);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnBook_WhenAnotherRequestChangedTheBook_ThrowsConcurrencyConflict()
    {
        var book = NewBook(available: 0, total: 1);
        var loan = new Loan { Id = 7, BookId = 3, Book = book, MemberId = 1, Member = NewMember() };
        _loans.Setup(l => l.GetForUpdateAsync(7, _ct)).ReturnsAsync(loan);
        _uow.Setup(u => u.SaveChangesAsync(_ct)).ThrowsAsync(new ConcurrencyConflictException("conflict"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CreateService().ReturnBookAsync(7, _ct));
    }
}
