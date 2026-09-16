namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>
/// Headline counters for the CMS dashboard. Totals are lifetime (excluding
/// soft-deleted rows); the <c>New*</c> counters and the <c>*Trend</c> percentages
/// are scoped to the selected window (and the equally long window before it).
/// </summary>
public class OverviewOutput
{
    public int RangeDays { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    // ── people ──
    public int Users { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int Riders { get; set; }
    public int Drivers { get; set; }
    public int VerifiedDrivers { get; set; }
    public int PendingDrivers { get; set; }
    public int OnlineDrivers { get; set; }
    public int DisabledUsers { get; set; }

    // ── fleet ──
    public int Vehicles { get; set; }
    public int VehicleSeats { get; set; }

    // ── supply (trips) ──
    public int Trips { get; set; }
    public int NewTrips { get; set; }
    public int ActiveTrips { get; set; }
    public int CompletedTrips { get; set; }
    public int CancelledTrips { get; set; }
    public int SeatsOffered { get; set; }
    public int SeatsTaken { get; set; }
    /// <summary>Seats taken ÷ seats offered, 0–1.</summary>
    public double SeatFillRate { get; set; }

    // ── demand (bookings + hail requests) ──
    public int Bookings { get; set; }
    public int NewBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public double BookingCancelRate { get; set; }
    public int RiderTrips { get; set; }
    public int NewRiderTrips { get; set; }
    public int OpenRiderTrips { get; set; }
    public int ClaimedRiderTrips { get; set; }
    public int ExpiredRiderTrips { get; set; }
    /// <summary>Matched ÷ all closed-or-open requests, 0–1.</summary>
    public double MatchRate { get; set; }
    public double AvgSeatsPerBooking { get; set; }

    // ── quality ──
    public int Ratings { get; set; }
    public double AvgRating { get; set; }
    public int LowRatings { get; set; }

    // ── engagement ──
    public int Notifications { get; set; }
    public int UnreadNotifications { get; set; }
    public int SavedPlaces { get; set; }
    public int ActiveSessions { get; set; }
    public int NewLogins { get; set; }

    // ── platform ──
    public int ApiCalls { get; set; }
    public int ApiErrors { get; set; }
    public double ApiErrorRate { get; set; }
    public double AvgResponseMs { get; set; }
    public int AuditEvents { get; set; }

    // ── period-over-period change (fraction, e.g. 0.12 = +12%; null when the
    //    previous window was empty and a percentage would be meaningless) ──
    public double? UsersTrend { get; set; }
    public double? TripsTrend { get; set; }
    public double? BookingsTrend { get; set; }
    public double? RiderTripsTrend { get; set; }
}
