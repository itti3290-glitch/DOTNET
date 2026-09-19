namespace Library.Core.Entities;

/// <summary>
/// A single borrowing of a <see cref="Book"/> by a <see cref="Member"/>.
/// Open while <see cref="ReturnedAt"/> is null. All times are UTC.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime LoanedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }
}
