using Library.Core.Entities;
using Library.Core.Exceptions;
using Library.Service.Services;

namespace Library.Tests.Services;

/// <summary>The inventory invariants on Book and the password hasher: pure logic, no mocks needed.</summary>
public class BookInvariantTests
{
    [Fact]
    public void TakeCopy_DecrementsAvailableCopies()
    {
        var book = new Book { TotalCopies = 2, AvailableCopies = 2 };
        book.TakeCopy();
        Assert.Equal(1, book.AvailableCopies);
    }

    [Fact]
    public void TakeCopy_AtZero_ThrowsAndStaysAtZero()
    {
        var book = new Book { Id = 5, TotalCopies = 1, AvailableCopies = 0 };
        Assert.Throws<NoCopiesAvailableException>(book.TakeCopy);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void ReturnCopy_CannotExceedTotalCopies()
    {
        var book = new Book { TotalCopies = 2, AvailableCopies = 2 };
        Assert.Throws<BusinessRuleException>(book.ReturnCopy);
        Assert.Equal(2, book.AvailableCopies);
    }

    [Fact]
    public void ReturnCopy_IncrementsWhenACopyIsOut()
    {
        var book = new Book { TotalCopies = 2, AvailableCopies = 1 };
        book.ReturnCopy();
        Assert.Equal(2, book.AvailableCopies);
    }
}

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_AcceptsTheRightPasswordAndRejectsAWrongOne()
    {
        var hash = _hasher.Hash("Admin123!");
        Assert.True(_hasher.Verify("Admin123!", hash));
        Assert.False(_hasher.Verify("admin123!", hash));
    }

    [Fact]
    public void Hash_UsesARandomSaltSoTheSamePasswordGivesDifferentHashes()
    {
        Assert.NotEqual(_hasher.Hash("same-password"), _hasher.Hash("same-password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("!SEED-PLACEHOLDER!")]
    public void Verify_RejectsMalformedHashesWithoutThrowing(string badHash)
    {
        Assert.False(_hasher.Verify("anything", badHash));
    }
}
