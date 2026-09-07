using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Support;

/// <summary>
/// One complaint or suggestion a user sent the support desk, and the desk's
/// answer to it.
///
/// Unlike <see cref="FaqItem"/> this holds no language pair. FAQ copy is
/// published to everyone and so has to exist in both languages; a complaint is
/// one person's own words, and the reply goes back to that one person — the
/// admin writes it in whatever language the submission arrived in, which
/// <see cref="Language"/> records so they do not have to guess.
///
/// The thread is deliberately one exchange deep: the user writes, the desk
/// answers, the row closes. A back-and-forth belongs in a messaging feature,
/// and pretending a single reply column is a conversation would leave replies
/// silently overwriting each other.
/// </summary>
public class Feedback : BaseEntity
{
    public FeedbackKind Kind { get; set; } = FeedbackKind.Complaint;

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// The trip this is about, when the user came in from one. Optional: a
    /// suggestion usually is not about a trip at all, and a complaint filed
    /// weeks later may no longer know which ride it was.
    /// </summary>
    public int? TripId { get; set; }
    public Trip? Trip { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The language the user was using when they wrote it, so the desk answers
    /// in the language they will read the answer in.
    /// </summary>
    public Language Language { get; set; } = Language.En;

    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

    /// <summary>The desk's answer, shown to the user as-is. Null until someone replies.</summary>
    public string? Reply { get; set; }

    public int? RepliedBy { get; set; }
    public DateTime? RepliedAt { get; set; }
}
