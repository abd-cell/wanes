using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Support;

/// <summary>
/// The few limits a complaint or suggestion has to respect. Shared by the input
/// validation, the EF column widths and the open-submission guard so the three
/// cannot drift into disagreeing about what a valid submission is.
/// </summary>
public static class FeedbackRules
{
    public const int MaxSubject = 150;

    /// <summary>
    /// Long enough for someone to actually describe what went wrong, short
    /// enough that a runaway paste cannot be used to fill the table. The column
    /// itself is unbounded — this is the door, not the shelf.
    /// </summary>
    public const int MaxMessage = 4000;

    public const int MaxReply = 4000;

    /// <summary>
    /// How many unanswered submissions one account may have waiting.
    ///
    /// Not rate limiting by clock — a genuine bad day can honestly produce
    /// three complaints in ten minutes, and a per-minute cap would block that
    /// while still letting a script file hundreds over a day. Capping what is
    /// *open* bounds the queue instead: the desk answers, and the user can
    /// write again.
    /// </summary>
    public const int MaxOpenPerUser = 5;

    /// <summary>Still with the desk — neither resolved nor dismissed.</summary>
    public static bool IsOpen(FeedbackStatus status) =>
        status is FeedbackStatus.New or FeedbackStatus.InReview;
}
