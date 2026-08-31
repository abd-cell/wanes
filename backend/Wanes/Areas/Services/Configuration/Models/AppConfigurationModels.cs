using System.ComponentModel.DataAnnotations;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Configuration.Models;

/// <summary>
/// The configuration as every client reads it. Deliberately flat and free of ids
/// or audit columns: the app fetches this before sign-in, so it must carry
/// nothing an anonymous caller shouldn't see.
/// </summary>
public class AppConfigurationOutput
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
    public CurrencyPosition CurrencyPosition { get; set; }
    public int CurrencyDecimals { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;

    // ── Support contact ──
    //
    // Null means "not configured"; the clients hide the channel rather than
    // rendering a row that goes nowhere.
    public string? SupportPhone { get; set; }
    public string? SupportWhatsApp { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportWebsite { get; set; }
    public string? SupportHours { get; set; }

    /// <summary>
    /// When the settings last changed. Clients cache the configuration to paint
    /// the right brand on a cold start; this tells them whether what they cached
    /// is still current.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public AppConfigurationOutput() { }

    public AppConfigurationOutput(Domain.Configuration.AppConfiguration e)
    {
        CurrencyCode = e.CurrencyCode;
        CurrencySymbol = e.CurrencySymbol;
        CurrencyPosition = e.CurrencyPosition;
        CurrencyDecimals = e.CurrencyDecimals;
        PrimaryColor = e.PrimaryColor;
        SupportPhone = e.SupportPhone;
        SupportWhatsApp = e.SupportWhatsApp;
        SupportEmail = e.SupportEmail;
        SupportWebsite = e.SupportWebsite;
        SupportHours = e.SupportHours;
        UpdatedAt = e.ModificationDate ?? e.CreationDate;
    }
}

/// <summary>Admin update payload. Every field is replaced — there is no partial save.</summary>
public class AppConfigurationInput
{
    [Required, StringLength(8, MinimumLength = 1)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required, StringLength(8, MinimumLength = 1)]
    public string CurrencySymbol { get; set; } = string.Empty;

    [EnumDataType(typeof(CurrencyPosition))]
    public CurrencyPosition CurrencyPosition { get; set; } = CurrencyPosition.Before;

    [Range(0, 3)]
    public int CurrencyDecimals { get; set; } = 2;

    /// <summary>
    /// `#RGB` or `#RRGGBB`. Normalised to the six-digit upper-case form on save so
    /// the clients only ever have to parse one shape.
    /// </summary>
    [Required, RegularExpression("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        ErrorMessage = "PrimaryColor must be a hex colour such as #0FAE9E.")]
    public string PrimaryColor { get; set; } = string.Empty;

    // ── Support contact ──
    //
    // Optional, so each pattern has to tolerate the empty string as well as null:
    // clearing a channel is a normal edit, not a validation failure.

    [StringLength(32)]
    [RegularExpression(@"^$|^\+?[0-9][0-9\s\-()]{4,}$",
        ErrorMessage = "SupportPhone must be a phone number such as +962790000000.")]
    public string? SupportPhone { get; set; }

    [StringLength(32)]
    [RegularExpression(@"^$|^\+?[0-9][0-9\s\-()]{4,}$",
        ErrorMessage = "SupportWhatsApp must be a phone number such as +962790000000.")]
    public string? SupportWhatsApp { get; set; }

    // Not [EmailAddress]: that attribute rejects the empty string, which would
    // make clearing the address impossible.
    [StringLength(256)]
    [RegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$",
        ErrorMessage = "SupportEmail must be an email address such as help@wanes.app.")]
    public string? SupportEmail { get; set; }

    [StringLength(512)]
    [RegularExpression(@"^$|^https?://\S+$",
        ErrorMessage = "SupportWebsite must be an http(s) URL.")]
    public string? SupportWebsite { get; set; }

    [StringLength(200)]
    public string? SupportHours { get; set; }
}
