namespace Wanes.Shareds.Enums;

/// <summary>
/// What a piece of feedback is: something went wrong, or something could be
/// better.
///
/// One entity carries both because the pipeline is identical — a user writes
/// it, the desk reads it, replies and closes it — and splitting them would give
/// the admin two inboxes to watch instead of one filter. The kind is what
/// changes the *reading*: a complaint is a promise the platform broke and gets
/// triaged first, a suggestion is an idea and can wait.
/// </summary>
public enum FeedbackKind
{
    Complaint = 1,
    Suggestion = 2,
}
