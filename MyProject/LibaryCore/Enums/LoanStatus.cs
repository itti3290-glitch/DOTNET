namespace LibaryCore.Enums;

/// <summary>Lifecycle of a <see cref="Entities.Loan"/>.</summary>
public enum LoanStatus
{
    /// <summary>The book is out with the member and not yet returned.</summary>
    Active = 0,

    /// <summary>The book was brought back.</summary>
    Returned = 1,

    /// <summary>Still out and past its due date.</summary>
    Overdue = 2
}
