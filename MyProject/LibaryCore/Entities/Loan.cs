using LibaryCore.Enums;

namespace LibaryCore.Entities;

/// <summary>
/// One borrow event: member X took book Y on a date, due on a date, maybe returned.
/// Sits between <see cref="User"/> and <see cref="Book"/> and carries the data of the
/// relationship (dates, status). Created only when a borrow attempt wins the race for an
/// <see cref="BookStatus.Available"/> book.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public Book Book { get; set; } = default!;

    /// <summary>The member who holds the book (a <see cref="User"/> with role Borrower).</summary>
    public int MemberId { get; set; }

    public User Member { get; set; } = default!;

    public DateTime BorrowedAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }

    /// <summary>Null while the book is still out.</summary>
    public DateTime? ReturnedAtUtc { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;
}
