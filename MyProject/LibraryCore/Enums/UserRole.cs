namespace LibraryCore.Enums;

/// <summary>
/// Application roles. Two roles with different real permissions:
/// Borrower can browse, borrow, return, reserve and cancel own reservations.
/// Manager can additionally manage the catalog (books, copies, categories) and see all loans.
/// </summary>
public enum UserRole
{
    Borrower = 0,
    Manager = 1
}
