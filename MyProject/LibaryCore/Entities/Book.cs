using System.ComponentModel.DataAnnotations;
using LibaryCore.Enums;

namespace LibaryCore.Entities;

/// <summary>
/// A book in the library - and the limited resource itself. There is exactly one row per
/// physical book, so its <see cref="Status"/> is what members compete over when they borrow.
/// </summary>
public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = default!;

    public string Author { get; set; } = default!;

    /// <summary>13-char ISBN, unique across the catalog.</summary>
    public string Isbn { get; set; } = default!;

    public int PublicationYear { get; set; }

    public string? Description { get; set; }

    public BookStatus Status { get; set; } = BookStatus.Available;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. SQL Server maintains this <c>rowversion</c> column
    /// automatically on every UPDATE. EF Core adds its old value to the WHERE clause, so an
    /// update based on stale data changes zero rows and raises <c>DbUpdateConcurrencyException</c>.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;

    // ----- Relationships -----

    /// <summary>One-to-many: the full borrow history of this book.</summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
