namespace LibaryCore.Enums;

/// <summary>
/// State of a <see cref="Entities.Book"/> - the limited resource of this system.
/// A borrow attempt is only valid when the book is <see cref="Available"/>.
/// Two members can try to borrow the same book at the same instant; only one may win.
/// </summary>
public enum BookStatus
{
    /// <summary>On the shelf, can be borrowed right now.</summary>
    Available = 0,

    /// <summary>Currently borrowed by a member.</summary>
    Loaned = 1,

    /// <summary>Withdrawn from circulation (damaged, lost, in repair).</summary>
    Maintenance = 2
}
