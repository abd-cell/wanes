using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Configuration;

/// <summary>
/// Platform-wide settings the admin owns from the CMS, read by every client.
///
/// Exactly one live row exists — <see cref="Wanes.DataAccess.Seeders.DataSeeder"/>
/// creates it and the service only ever updates it, so there is nothing to pick
/// between when a client asks for "the" configuration.
/// </summary>
public class AppConfiguration : AuditableEntity
{
    // ── Currency ──

    /// <summary>ISO 4217-style code shown next to amounts where the symbol is ambiguous ("JOD").</summary>
    public string CurrencyCode { get; set; } = "JOD";

    /// <summary>What actually renders beside a price ("£", "د.أ", "$").</summary>
    public string CurrencySymbol { get; set; } = "د.أ";

    public CurrencyPosition CurrencyPosition { get; set; } = CurrencyPosition.After;

    /// <summary>Fraction digits, 0–3. Zero suits currencies with no minor unit.</summary>
    public int CurrencyDecimals { get; set; } = 3;

    // ── Branding ──

    /// <summary>
    /// Brand primary as `#RRGGBB`. The clients derive every other shade from it
    /// (hover/ink, the dark-theme variant, tints, and the foreground that sits on
    /// a solid fill), so this single value re-skins both the app and the CMS.
    /// </summary>
    public string PrimaryColor { get; set; } = "#0FAE9E";

    // ── Support contact ──
    //
    // Every channel below is optional and starts unset: a fresh install has no
    // support desk yet, and the app hides a channel it has no value for rather
    // than offering the user a dead link. `null` and `""` mean the same thing to
    // a client, so the service stores the empty form as null.

    /// <summary>Support line in E.164 (`+962790000000`), dialled from the app.</summary>
    public string? SupportPhone { get; set; }

    /// <summary>WhatsApp number in E.164. Often the same as <see cref="SupportPhone"/>, but not always.</summary>
    public string? SupportWhatsApp { get; set; }

    public string? SupportEmail { get; set; }

    /// <summary>Help centre or company site, as an absolute `http(s)` URL.</summary>
    public string? SupportWebsite { get; set; }

    /// <summary>
    /// Free text, shown verbatim under the channels ("Sun–Thu, 9:00–17:00").
    /// Deliberately not structured: opening hours vary by country and season far
    /// more than a schema would usefully capture.
    /// </summary>
    public string? SupportHours { get; set; }
}
