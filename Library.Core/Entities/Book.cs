using Library.Core.Exceptions;

namespace Library.Core.Entities;

/// <summary>
/// A book title. This is the limited resource members compete for:
/// <see cref="AvailableCopies"/> is decremented on loan and must stay within 0..<see cref="TotalCopies"/>.
/// Optimistic concurrency is handled by PostgreSQL's <c>xmin</c> system column, configured in
/// BookConfiguration as a shadow property, so there is no token property to maintain here.
/// </summary>
public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Isbn { get; set; } = string.Empty;

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    /// <summary>Takes one copy. Throws if none are available.</summary>
    public void TakeCopy()
    {
        if (AvailableCopies <= 0)
            throw new NoCopiesAvailableException(Id);
        AvailableCopies--;
    }

    /// <summary>Puts one copy back. Throws if that would exceed the total.</summary>
    public void ReturnCopy()
    {
        if (AvailableCopies >= TotalCopies)
            throw new BusinessRuleException($"Book {Id} already has all copies in the library.");
        AvailableCopies++;
    }
}
