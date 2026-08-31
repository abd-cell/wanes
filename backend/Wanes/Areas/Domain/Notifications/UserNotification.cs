using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Notifications;

/// <summary>
/// A stored notification shown in the user's inbox (also pushed via FCM + SSE).
///
/// Both languages live on the row so the inbox can follow the reader's current
/// language with no round trip, and stays correct if they switch language after
/// the fact — the same reasoning as <c>FaqItem</c>. Unlike FAQ, the Arabic half
/// is nullable: an admin composing one by hand may only have typed English, and
/// readers then fall back to <see cref="Title"/>/<see cref="Body"/>.
/// </summary>
public class UserNotification : BaseEntity
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }

    /// <summary>Default wording — English for system messages.</summary>
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? TitleAr { get; set; }
    public string? BodyAr { get; set; }

    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
}
