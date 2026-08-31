using Wanes.Areas.Domain.Notifications;

namespace Wanes.Areas.Services.Notifications.Models;

public class NotificationRow
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Arabic wording, when the sender had one. Both languages are sent so the
    /// inbox can follow the reader's current language with no round trip — the
    /// push copy has to commit to one, but the feed does not.
    /// </summary>
    public string? TitleAr { get; set; }

    public string? BodyAr { get; set; }

    /// <summary>Raw JSON payload (trip/request ids) — the app uses it to deep-link on tap.</summary>
    public string? Data { get; set; }

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
        TitleAr = notification.TitleAr;
        BodyAr = notification.BodyAr;
        Data = notification.DataJson;
        IsRead = notification.IsRead;
        CreationDate = notification.CreationDate;
    }
}
