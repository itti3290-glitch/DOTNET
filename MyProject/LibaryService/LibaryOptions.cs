namespace LibaryService;

/// <summary>
/// Tunable library policy, supplied by the host at startup (bound from the <c>Library</c>
/// configuration section). Injected directly - no <c>IOptions</c> wrapper - because it never
/// changes after startup.
/// </summary>
public sealed class LibaryOptions
{
    /// <summary>How many days a borrowed book is due in.</summary>
    public int LoanPeriodDays { get; set; } = 14;

    /// <summary>How many active loans a single member may hold at once.</summary>
    public int MaxActiveLoansPerMember { get; set; } = 5;
}
