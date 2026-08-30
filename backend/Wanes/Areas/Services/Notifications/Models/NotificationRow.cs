using Wanes.Areas.Domain.Notifications;

namespace Wanes.Areas.Services.Notifications.Models;

public class NotificationRow
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreationDate { get; set; }

    public NotificationRow() { }

    public NotificationRow(UserNotification notification)
    {
        if (notification == null) return;

        Id = notification.Id;
        Type = notification.Type.ToString();
        Title = notification.Title;
        Body = notification.Body;
        IsRead = notification.IsRead;
        CreationDate = notification.CreationDate;
    }
}
