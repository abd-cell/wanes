using Wanes.Areas.Domain.Support;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Support.Models;

/// <summary>
/// One FAQ entry as the clients read it.
///
/// Both languages ship on every entry instead of the server picking one. The
/// mobile app sends a fixed `Accept-Language: en`, so negotiating server-side
/// would quietly serve English to an Arabic user; and the app already bundles
/// both string tables and switches locale without a round trip, so handing it
/// the pair keeps the help screen consistent with the rest of the UI.
/// </summary>
public class FaqItemOutput
{
    public int Id { get; set; }
    public FaqCategory Category { get; set; }
    public string QuestionEn { get; set; } = string.Empty;
    public string QuestionAr { get; set; } = string.Empty;
    public string AnswerEn { get; set; } = string.Empty;
    public string AnswerAr { get; set; } = string.Empty;

    public FaqItemOutput() { }

    public FaqItemOutput(FaqItem e)
    {
        Id = e.Id;
        Category = e.Category;
        QuestionEn = e.QuestionEn;
        QuestionAr = e.QuestionAr;
        AnswerEn = e.AnswerEn;
        AnswerAr = e.AnswerAr;
    }
}

/// <summary>
/// The whole published FAQ in one response, already ordered.
///
/// It is a short list that every client renders in full, so paging it would only
/// add round trips. <see cref="UpdatedAt"/> lets a client tell whether what it
/// cached is still current.
/// </summary>
public class FaqOutput
{
    public IReadOnlyList<FaqItemOutput> Items { get; set; } = [];

    /// <summary>Most recent change across the published entries; null when there are none.</summary>
    public DateTime? UpdatedAt { get; set; }
}
