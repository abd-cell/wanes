using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Notifications;

/// <summary>
/// A notification's wording in both languages.
///
/// The Arabic half is nullable on purpose, which is where this differs from
/// <c>FaqItem</c>'s mandatory <c>QuestionEn</c>/<c>QuestionAr</c> pair: system
/// notifications always render both from <see cref="NotificationTexts"/>, but an
/// admin composing one by hand may only have typed English. A missing Arabic
/// variant falls back to the default rather than showing the reader nothing.
/// </summary>
public sealed class LocalizedText
{
    /// <summary>Default wording — English for system messages, whatever the admin typed otherwise.</summary>
    public required string Title { get; init; }

    public required string Body { get; init; }

    public string? TitleAr { get; init; }

    public string? BodyAr { get; init; }

    /// <summary>Picks the wording for one reader, falling back when Arabic is absent.</summary>
    public (string Title, string Body) For(Language language) =>
        language == Language.Ar
            ? (string.IsNullOrWhiteSpace(TitleAr) ? Title : TitleAr,
               string.IsNullOrWhiteSpace(BodyAr) ? Body : BodyAr)
            : (Title, Body);

    /// <summary>Wording an admin typed, with an optional Arabic variant.</summary>
    public static LocalizedText Raw(string title, string body, string? titleAr = null, string? bodyAr = null) =>
        new() { Title = title, Body = body, TitleAr = titleAr, BodyAr = bodyAr };
}
