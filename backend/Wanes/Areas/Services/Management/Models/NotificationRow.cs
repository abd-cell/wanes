using Wanes.Areas.Domain.Notifications;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a user notification (list + detail share one shape).</summary>
public class NotificationRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string? BodyAr { get; set; }
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// Cleared — by the owner off their own inbox, or by an admin off this
    /// table. Either way the row is still listed here; the console is the only
    /// place a deleted notification is visible. The audit log
    /// (<c>notification.delete</c> vs <c>admin.notifications.delete</c>) says
    /// which of the two it was.
    /// </summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletionDate { get; set; }

    public NotificationRow() { }

    public NotificationRow(UserNotification e)
    {
        Id = e.Id;
        UserId = e.UserId;
        Type = e.Type;
        Title = e.Title;
        Body = e.Body;
        TitleAr = e.TitleAr;
        BodyAr = e.BodyAr;
        DataJson = e.DataJson;
        IsRead = e.IsRead;
        CreationDate = e.CreationDate;
        IsDeleted = e.IsDeleted;
        DeletionDate = e.DeletionDate;
    }
}

/// <summary>One notification aimed at a whole audience rather than a single user.</summary>
public class BroadcastInput
{
    public NotificationAudience Audience { get; set; } = NotificationAudience.All;
    public NotificationType Type { get; set; } = NotificationType.General;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Arabic wording. Optional — admin-typed text cannot be machine-translated,
    /// so recipients reading Arabic fall back to <see cref="Title"/>/<see cref="Body"/>
    /// when it is left blank.
    /// </summary>
    public string? TitleAr { get; set; }

    public string? BodyAr { get; set; }

    public string? DataJson { get; set; }
}

/// <summary>What a broadcast reached, so the console can report it back.</summary>
public class BroadcastResult
{
    public NotificationAudience Audience { get; set; }
    public int Recipients { get; set; }
}

/// <summary>Create/update payload for a single user's notification.</summary>
public class NotificationInput
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional Arabic wording; blank means Arabic readers see the default.</summary>
    public string? TitleAr { get; set; }

    public string? BodyAr { get; set; }

    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>
/// One notification aimed at a hand-picked set of users. Sits between
/// <see cref="NotificationInput"/> (exactly one recipient) and
/// <see cref="BroadcastInput"/> (a whole audience) — the case an admin hits when
/// a message concerns a specific handful of people, e.g. everyone on one trip.
/// </summary>
public class TargetedSendInput
{
    public List<int> UserIds { get; set; } = [];
    public NotificationType Type { get; set; } = NotificationType.General;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional Arabic wording; blank means Arabic readers see the default.</summary>
    public string? TitleAr { get; set; }

    public string? BodyAr { get; set; }

    public string? DataJson { get; set; }
}

/// <summary>What a targeted send reached, so the console can report it back.</summary>
public class TargetedSendResult
{
    public int Recipients { get; set; }
}

/// <summary>An action applied to a set of inbox rows the admin ticked in the table.</summary>
public class BulkNotificationInput
{
    public List<int> Ids { get; set; } = [];
    public NotificationBulkAction Action { get; set; }
}

/// <summary>How many rows a bulk action actually changed.</summary>
public class BulkNotificationResult
{
    public NotificationBulkAction Action { get; set; }
    public int Affected { get; set; }
}

/// <summary>One slice of the by-type breakdown on the notification manager.</summary>
public class NotificationTypeCount
{
    public NotificationType Type { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Headline counts for the notification manager. Deliberately a separate call
/// from the paged list: the totals describe the whole table, not the page the
/// admin happens to be looking at.
/// </summary>
public class NotificationStats
{
    public int Total { get; set; }
    public int Unread { get; set; }
    public int Read { get; set; }

    /// <summary>Rows cleared by their owner or by an admin; still counted in <see cref="Total"/>.</summary>
    public int Deleted { get; set; }

    /// <summary>Rows created in the last 24 hours — "what did we just send?".</summary>
    public int Last24Hours { get; set; }

    /// <summary>Distinct users holding at least one notification.</summary>
    public int Recipients { get; set; }

    /// <summary>Busiest types first; empty types are omitted.</summary>
    public List<NotificationTypeCount> ByType { get; set; } = [];
}
