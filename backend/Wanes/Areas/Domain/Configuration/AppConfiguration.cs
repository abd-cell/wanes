using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
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

    /// <summary>
    /// The display/body typeface, as one of a closed set of script pairings —
    /// see <see cref="AppFont"/> for why it is an enum and not a family name.
    /// Together with <see cref="PrimaryColor"/> this is the whole re-skin
    /// surface: colour and type.
    /// </summary>
    public AppFont FontFamily { get; set; } = AppFont.Jakarta;

    // ── Matching and lifecycle windows ──

    /// <summary>
    /// How long before departure a driver must have answered the run-or-cancel
    /// question on a trip short of its seat threshold. Riders stood down have to
    /// learn in time to find another ride, and how much time that takes is local.
    /// </summary>
    public int ConfirmCutoffMinutes { get; set; } = TripConfirmationRules.DefaultCutoffMinutes;

    /// <summary>How long before that cutoff the driver is asked.</summary>
    public int ConfirmDecisionLeadMinutes { get; set; } = TripConfirmationRules.DefaultDecisionLeadMinutes;

    /// <summary>
    /// How many passengers a new trip asks for before it confirms, when the
    /// driver expresses no preference.
    ///
    /// Admin-set because "enough passengers to be worth driving" is a
    /// marketplace fact — a city, a fuel price, a typical leg — and a driver
    /// filling in a form is the wrong person to be deciding it from scratch.
    /// It only ever **seeds** the trip's own <c>MinSeatsToConfirm</c>: changing
    /// it re-decides nothing that already exists.
    /// </summary>
    public int MinimumPassengersDefault { get; set; } = TripConfirmationRules.DefaultMinimumPassengers;

    /// <summary>
    /// How long a ride request collects driver offers before one is selected.
    ///
    /// **Zero — the shipped value — means the first interested driver is
    /// selected immediately**, which is first-come-first-served and exactly what
    /// the old claim did. Raising it turns the same data into competing offers
    /// ranked deterministically, with no domain change: that is the whole reason
    /// it is a setting rather than a rule in code.
    /// </summary>
    public int DriverSelectionWindowMinutes { get; set; } = DriverSelectionRules.ImmediateSelection;

    /// <summary>
    /// The average speed the duration estimate assumes, in km/h.
    ///
    /// Drives the earliest departure a rider may post for — one leg-time per
    /// seat, because a driver has to gather everybody — and the arrival estimate
    /// the rider is shown. Admin-set because what counts as normal progress is a
    /// city question, not a constant: Amman in the afternoon is not a motorway.
    /// </summary>
    public double AverageSpeedKmh { get; set; } = RiderTripRules.DefaultAverageSpeedKmh;

    // ── Fare display ──
    //
    // There are no payments: these only decide what a trip is *listed* at. They
    // exist because a driver claiming a rider's posting never quoted a rate,
    // they only agreed to go — so the price sheet opens on a figure derived
    // from these rates rather than on a blank. See Trips.FareRules.

    /// <summary>Flag-fall for a derived per-seat price, in the platform currency.</summary>
    public decimal FareBaseAmount { get; set; } = FareRules.DefaultBaseAmount;

    /// <summary>Per-kilometre rate on top of <see cref="FareBaseAmount"/>.</summary>
    public decimal FarePerKm { get; set; } = FareRules.DefaultPerKm;

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
