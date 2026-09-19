using Library.Core.Enums;

namespace Library.Core.Entities;

/// <summary>
/// A library member who can borrow books. Also the login identity for the API.
/// </summary>
public class Member
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
