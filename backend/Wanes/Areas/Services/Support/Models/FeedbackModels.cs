using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Support;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Support.Models;

/// <summary>
/// What a user sends the support desk.
///
/// No status and no reply: those are the desk's side of the row, and accepting
/// them here would let a client mark its own complaint resolved.
/// </summary>
public class FeedbackInput
{
    [EnumDataType(typeof(FeedbackKind))]
    public FeedbackKind Kind { get; set; } = FeedbackKind.Complaint;

    /// <summary>The trip this is about, when there is one. Must belong to the caller.</summary>
    public int? TripId { get; set; }

    [Required, StringLength(FeedbackRules.MaxSubject, MinimumLength = 3)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(FeedbackRules.MaxMessage, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// One of the user's own submissions, with the desk's answer if it has come.
///
/// The reply travels as written rather than as a template: it is a person
/// answering a person, so there is nothing to localise — which is why the
/// server records the submission's language and the admin answers in it.
/// </summary>
public class FeedbackOutput
{
    public int Id { get; set; }
    public FeedbackKind Kind { get; set; }
    public FeedbackStatus Status { get; set; }
    public int? TripId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreationDate { get; set; }

    public FeedbackOutput() { }

    public FeedbackOutput(Feedback e)
    {
        Id = e.Id;
        Kind = e.Kind;
        Status = e.Status;
        TripId = e.TripId;
        Subject = e.Subject;
        Message = e.Message;
        Reply = e.Reply;
        RepliedAt = e.RepliedAt;
        CreationDate = e.CreationDate;
    }
}
