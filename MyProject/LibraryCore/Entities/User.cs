using LibraryCore.Enums;

namespace LibraryCore.Entities;

/// <summary>
/// A registered account. One table holds both kinds of user - the <see cref="Role"/> field
/// decides whether this is a <see cref="UserRole.Borrower"/> or a <see cref="UserRole.Manager"/>.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = default!;

    public string Email { get; set; } = default!;

    /// <summary>Salted hash only - the plain password is never stored or logged.</summary>
    public string PasswordHash { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public UserRole Role { get; set; } = UserRole.Borrower;

    public DateTime CreatedAtUtc { get; set; }

    // ----- Relationships -----

    /// <summary>One-to-many: every loan taken by this member. "The books the user borrows"
    /// is a query over this collection, not a field of its own.</summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
