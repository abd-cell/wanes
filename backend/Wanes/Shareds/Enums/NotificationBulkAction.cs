namespace Wanes.Shareds.Enums;

/// <summary>
/// What an admin is doing to a hand-picked set of inbox rows from the console.
///
/// Read state is included alongside deletion because an admin clearing a bad
/// batch usually wants it *out of the badge count* rather than gone — marking
/// read leaves the audit trail intact, which soft-deleting the rows does not.
/// </summary>
public enum NotificationBulkAction
{
    MarkRead = 1,
    MarkUnread = 2,
    Delete = 3,
}
