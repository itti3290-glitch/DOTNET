namespace Library.Core.Enums;

public enum UserRole
{
    Member = 0,
    Admin = 1
}

public enum LoanStatus
{
    Active = 0,
    Returned = 1
}

/// <summary>Whitelist of columns books can be sorted by (callers can't sort on arbitrary fields).</summary>
public enum BookSortBy
{
    Title = 0,
    Author = 1,
    Availability = 2
}
