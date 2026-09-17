using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
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

    /// <summary>
    /// The display/body typeface. A client maps the value to the concrete faces
    /// it uses for Latin and Arabic; it is never a family name on the wire.
    /// </summary>
    public AppFont FontFamily { get; set; }

    /// <summary>
    /// Minutes before departure a seat threshold has to be decided, and minutes
    /// before that the driver is asked. Both are on the wire because both draw
    /// clocks: the driver's card counts down to the decision, and the rider's
    /// seat says when they will know whether the trip runs.
    /// </summary>
    public int ConfirmCutoffMinutes { get; set; }

    public int ConfirmDecisionLeadMinutes { get; set; }

    /// <summary>
    /// How many passengers a new trip asks for before it confirms, unless its
    /// driver says otherwise. On the wire because the posting form has to open
    /// on the marketplace's number rather than on 1 — a driver who never looks
    /// at the field should still get the platform's answer, and the app cannot
    /// guess it.
    /// </summary>
    public int MinimumPassengersDefault { get; set; }

    /// <summary>
    /// How long a ride request collects driver offers before one is selected.
    /// Zero — the shipped value — means the first interested driver gets it
    /// immediately. On the wire because the driver's card has to know whether
    /// tapping "I can drive this" hands them a trip or an offer in a queue.
    /// </summary>
    public int DriverSelectionWindowMinutes { get; set; }

    /// <summary>
    /// The average speed behind every duration estimate. Sent because the app
    /// computes the earliest departure a rider may post for *before* it calls
    /// the server — a rider should be told "not before 08:40" while they are
    /// picking the time, not refused after they tap Post.
    /// </summary>
    public double AverageSpeedKmh { get; set; }

    /// <summary>
    /// Flag-fall and per-kilometre rate behind a derived per-seat price.
    ///
    /// Sent to the clients as well as used on the server, because both quote the
    /// figure: the driver's hail card shows what a request is worth *before*
    /// anyone accepts it, and the trip the server prices on accept has to agree
    /// with the estimate that driver was looking at. Hardcoding the rates in the
    /// app is what let those two drift.
    /// </summary>
    public decimal FareBaseAmount { get; set; }

    public decimal FarePerKm { get; set; }

    // ── Shared marketplace, reliability, safety — see AppConfiguration ──
    public int ScheduledSelectionWindowMinutes { get; set; }
    public bool RiderOfferChoice { get; set; }
    public bool RequireSharedTermsAcceptance { get; set; }
    public int FreeCancelGraceMinutes { get; set; }
    public int LateCancelLeadMinutes { get; set; }
    public int ReliabilityWarnPoints { get; set; }
    public int ReliabilitySuspendPoints { get; set; }
    public int ReliabilityWindowDays { get; set; }
    public int SuspensionDays { get; set; }
    public bool BoardingCodeRequired { get; set; }
    public string EmergencyNumber { get; set; } = string.Empty;
    public string? ShareBaseUrl { get; set; }

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
        FontFamily = e.FontFamily;
        ConfirmCutoffMinutes = e.ConfirmCutoffMinutes;
        ConfirmDecisionLeadMinutes = e.ConfirmDecisionLeadMinutes;
        MinimumPassengersDefault = e.MinimumPassengersDefault;
        DriverSelectionWindowMinutes = e.DriverSelectionWindowMinutes;
        AverageSpeedKmh = e.AverageSpeedKmh;
        FareBaseAmount = e.FareBaseAmount;
        FarePerKm = e.FarePerKm;
        ScheduledSelectionWindowMinutes = e.ScheduledSelectionWindowMinutes;
        RiderOfferChoice = e.RiderOfferChoice;
        RequireSharedTermsAcceptance = e.RequireSharedTermsAcceptance;
        FreeCancelGraceMinutes = e.FreeCancelGraceMinutes;
        LateCancelLeadMinutes = e.LateCancelLeadMinutes;
        ReliabilityWarnPoints = e.ReliabilityWarnPoints;
        ReliabilitySuspendPoints = e.ReliabilitySuspendPoints;
        ReliabilityWindowDays = e.ReliabilityWindowDays;
        SuspensionDays = e.SuspensionDays;
        BoardingCodeRequired = e.BoardingCodeRequired;
        EmergencyNumber = e.EmergencyNumber;
        ShareBaseUrl = e.ShareBaseUrl;
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

    /// <summary>
    /// One of <see cref="AppFont"/>. A value outside the set is rejected rather
    /// than coerced: it means the caller knows a font this build does not, and
    /// storing it would leave every client without a face it can resolve.
    /// </summary>
    [EnumDataType(typeof(AppFont))]
    public AppFont FontFamily { get; set; } = AppFont.Jakarta;

    /// <summary>
    /// How long before departure a seat threshold has to be decided. The floor
    /// is what makes the answer useful to a stood-down rider; the ceiling stops
    /// a trip being called off while it was still filling.
    /// </summary>
    [Range(TripConfirmationRules.MinCutoffMinutes, TripConfirmationRules.MaxCutoffMinutes)]
    public int ConfirmCutoffMinutes { get; set; } = TripConfirmationRules.DefaultCutoffMinutes;

    /// <summary>How much warning the driver gets before that cutoff.</summary>
    [Range(TripConfirmationRules.MinDecisionLeadMinutes, TripConfirmationRules.MaxDecisionLeadMinutes)]
    public int ConfirmDecisionLeadMinutes { get; set; } = TripConfirmationRules.DefaultDecisionLeadMinutes;

    /// <summary>
    /// The passenger threshold a new trip starts with. Bounded above by what an
    /// ordinary car can seat: a default no vehicle can reach would post trips
    /// that can never confirm.
    /// </summary>
    [Range(TripConfirmationRules.NoThreshold, TripConfirmationRules.MaxMinimumPassengers)]
    public int MinimumPassengersDefault { get; set; } = TripConfirmationRules.DefaultMinimumPassengers;

    /// <summary>
    /// Minutes a ride request collects offers before a driver is selected. Zero
    /// keeps first-come-first-served, which is what ships; the ceiling is there
    /// because riders waiting on a decision the marketplace could have made for
    /// them is the failure this window trades against.
    /// </summary>
    [Range(DriverSelectionRules.ImmediateSelection, DriverSelectionRules.MaxSelectionWindowMinutes)]
    public int DriverSelectionWindowMinutes { get; set; } = DriverSelectionRules.ImmediateSelection;

    /// <summary>
    /// Average speed for duration estimates, in km/h. Bounded because it divides:
    /// zero would make every estimate infinite, and an unrealistic ceiling would
    /// let a rider post a four-seat trip leaving in ten minutes.
    /// </summary>
    [Range(RiderTripRules.MinAverageSpeedKmh, RiderTripRules.MaxAverageSpeedKmh)]
    public double AverageSpeedKmh { get; set; } = RiderTripRules.DefaultAverageSpeedKmh;

    /// <summary>
    /// Flag-fall for a derived per-seat price. Zero is allowed and means the
    /// whole fare comes from the distance; negative is not, because it would
    /// price a long ride below a short one.
    /// </summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal FareBaseAmount { get; set; } = FareRules.DefaultBaseAmount;

    /// <summary>
    /// Per-kilometre rate. Zero is allowed — it makes every derived price the
    /// flat flag-fall, which is a legitimate choice for a small town.
    /// </summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal FarePerKm { get; set; } = FareRules.DefaultPerKm;

    // ── Shared marketplace ──

    [Range(DriverSelectionRules.ImmediateSelection, DriverSelectionRules.MaxSelectionWindowMinutes)]
    public int ScheduledSelectionWindowMinutes { get; set; } = DriverSelectionRules.DefaultScheduledWindowMinutes;

    public bool RiderOfferChoice { get; set; } = true;
    public bool RequireSharedTermsAcceptance { get; set; } = true;

    // ── Reliability ──

    [Range(0, ReliabilityRules.MaxGraceMinutes)]
    public int FreeCancelGraceMinutes { get; set; } = ReliabilityRules.DefaultFreeCancelGraceMinutes;

    [Range(0, ReliabilityRules.MaxLateLeadMinutes)]
    public int LateCancelLeadMinutes { get; set; } = ReliabilityRules.DefaultLateCancelLeadMinutes;

    [Range(1, ReliabilityRules.MaxPoints)]
    public int ReliabilityWarnPoints { get; set; } = ReliabilityRules.DefaultWarnPoints;

    [Range(1, ReliabilityRules.MaxPoints)]
    public int ReliabilitySuspendPoints { get; set; } = ReliabilityRules.DefaultSuspendPoints;

    [Range(1, ReliabilityRules.MaxWindowDays)]
    public int ReliabilityWindowDays { get; set; } = ReliabilityRules.DefaultWindowDays;

    [Range(0, ReliabilityRules.MaxSuspensionDays)]
    public int SuspensionDays { get; set; } = ReliabilityRules.DefaultSuspensionDays;

    // ── Safety ──

    public bool BoardingCodeRequired { get; set; } = true;

    [Required, StringLength(16, MinimumLength = 2)]
    [RegularExpression(@"^\+?[0-9]{2,15}$", ErrorMessage = "EmergencyNumber must be digits, such as 911.")]
    public string EmergencyNumber { get; set; } = "911";

    [StringLength(512)]
    [RegularExpression(@"^$|^https?://\S+$", ErrorMessage = "ShareBaseUrl must be an http(s) URL.")]
    public string? ShareBaseUrl { get; set; }

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
