using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Support;

/// <summary>
/// One question-and-answer pair the admin writes in the CMS and every client
/// reads from the help screen.
///
/// Both languages live on the same row rather than in a per-language child
/// table: an entry only makes sense as a matched pair, and keeping them
/// together means an admin can never publish an English answer whose Arabic
/// half silently went missing.
/// </summary>
public class FaqItem : AuditableEntity
{
    public FaqCategory Category { get; set; } = FaqCategory.General;

    public string QuestionEn { get; set; } = string.Empty;
    public string QuestionAr { get; set; } = string.Empty;
    public string AnswerEn { get; set; } = string.Empty;
    public string AnswerAr { get; set; } = string.Empty;

    /// <summary>
    /// Manual order inside a category, ascending. The admin decides what a new
    /// rider should read first, which no automatic ordering can infer — ties
    /// fall back to <see cref="BaseEntity.Id"/> so the list never reshuffles
    /// between requests.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Hides the entry from the clients without deleting it, so an answer can be
    /// drafted or retired without losing the text. Admins always see every row.
    /// </summary>
    public bool IsPublished { get; set; } = true;
}
