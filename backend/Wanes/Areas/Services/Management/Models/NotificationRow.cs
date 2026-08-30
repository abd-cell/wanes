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
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreationDate { get; set; }

    public NotificationRow() { }

    public NotificationRow(UserNotification e)
    {
        Id = e.Id;
        UserId = e.UserId;
        Type = e.Type;
        Title = e.Title;
        Body = e.Body;
        DataJson = e.DataJson;
        IsRead = e.IsRead;
        CreationDate = e.CreationDate;
    }
}

/// <summary>Create/update payload for a user notification.</summary>
public class NotificationInput
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
}
