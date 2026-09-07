namespace Wanes.Shareds.Enums;

/// <summary>
/// Where a submission stands with the support desk.
///
/// The user sees this verbatim, so it is worded as an answer to "what is
/// happening to my complaint?" rather than as an internal queue state. That is
/// also why <see cref="Dismissed"/> exists next to <see cref="Resolved"/>:
/// "we read it and are not acting on it" is a real outcome, and dressing it up
/// as resolved would be a lie the user can spot.
/// </summary>
public enum FeedbackStatus
{
    /// <summary>Submitted, nobody has picked it up yet.</summary>
    New = 1,

    /// <summary>An admin has it open and is working on it.</summary>
    InReview = 2,

    /// <summary>Dealt with. Terminal.</summary>
    Resolved = 3,

    /// <summary>Read and closed without action — out of scope, duplicate, or not a defect. Terminal.</summary>
    Dismissed = 4,
}
