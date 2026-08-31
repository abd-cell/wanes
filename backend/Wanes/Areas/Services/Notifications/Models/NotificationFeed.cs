namespace Wanes.Areas.Services.Notifications.Models;

/// <summary>The inbox payload: the newest page plus the badge count.</summary>
public class NotificationFeed
{
    public List<NotificationRow> Items { get; set; } = [];
    public int UnreadCount { get; set; }
}
