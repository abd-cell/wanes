using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Support;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a FAQ entry (list + detail share one shape).</summary>
public class FaqRow
{
    public int Id { get; set; }
    public FaqCategory Category { get; set; }
    public string QuestionEn { get; set; } = string.Empty;
    public string QuestionAr { get; set; } = string.Empty;
    public string AnswerEn { get; set; } = string.Empty;
    public string AnswerAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreationDate { get; set; }

    public FaqRow() { }

    public FaqRow(FaqItem e)
    {
        Id = e.Id;
        Category = e.Category;
        QuestionEn = e.QuestionEn;
        QuestionAr = e.QuestionAr;
        AnswerEn = e.AnswerEn;
        AnswerAr = e.AnswerAr;
        SortOrder = e.SortOrder;
        IsPublished = e.IsPublished;
        CreationDate = e.CreationDate;
    }
}

/// <summary>
/// Create/update payload for a FAQ entry.
///
/// Both languages are required: a published entry with half its text missing
/// renders as a blank answer for one audience, which is worse than not shipping
/// the entry at all. Use <see cref="IsPublished"/> to draft instead.
/// </summary>
public class FaqInput
{
    [EnumDataType(typeof(FaqCategory))]
    public FaqCategory Category { get; set; } = FaqCategory.General;

    [Required, StringLength(300, MinimumLength = 1)]
    public string QuestionEn { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 1)]
    public string QuestionAr { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string AnswerEn { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string AnswerAr { get; set; } = string.Empty;

    [Range(0, 9999)]
    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;
}
