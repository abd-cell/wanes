using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Support;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>
/// Admin view of a complaint or suggestion (list + detail share one shape).
///
/// Carries the whole message, not a preview: the desk cannot triage what it
/// cannot read, and the grid opens the row to reply anyway.
/// </summary>
public class FeedbackRow
{
    public int Id { get; set; }
    public FeedbackKind Kind { get; set; }
    public FeedbackStatus Status { get; set; }

    public int UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Answer in this language — it is the one the user was reading the app in.</summary>
    public Language Language { get; set; }

    public int? TripId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Reply { get; set; }
    public string? RepliedByName { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreationDate { get; set; }

    public FeedbackRow() { }

    public FeedbackRow(Feedback e)
    {
        Id = e.Id;
        Kind = e.Kind;
        Status = e.Status;
        UserId = e.UserId;
        Language = e.Language;
        TripId = e.TripId;
        Subject = e.Subject;
        Message = e.Message;
        Reply = e.Reply;
        RepliedAt = e.RepliedAt;
        CreationDate = e.CreationDate;
    }
}

/// <summary>
/// What an admin may change on a submission: where it stands, and what to say
/// back.
///
/// Everything else is the user's own text and stays read-only — an editable
/// complaint is not evidence of anything. There is no create payload for the
/// same reason: the desk does not file complaints on someone's behalf.
/// </summary>
public class FeedbackReviewInput
{
    [EnumDataType(typeof(FeedbackStatus))]
    public FeedbackStatus Status { get; set; } = FeedbackStatus.InReview;

    /// <summary>
    /// Optional, so a submission can be picked up (New → InReview) before there
    /// is anything to say. Blank leaves any existing reply untouched rather
    /// than erasing it — a status change is not a retraction.
    /// </summary>
    [StringLength(FeedbackRules.MaxReply)]
    public string? Reply { get; set; }
}
