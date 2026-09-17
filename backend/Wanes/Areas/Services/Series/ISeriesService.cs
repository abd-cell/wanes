using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Series.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Series;

/// <summary>
/// Committing to a whole recurring schedule: a driver taking a rider's
/// commute, a rider booking every day of a driver's. See
/// <see cref="Domain.Series.SeriesCommitment"/> and <see cref="Domain.Series.SeriesRules"/>.
/// </summary>
[TransientInjectable]
public interface ISeriesService
{
    // ── Driver serves a rider's series ──

    /// <summary>A driver offers to drive every day of the recurring request this card belongs to.</summary>
    Task<BaseResponse<SeriesRow>> Propose(int rideRequestId, ProposeSeriesInput input);

    /// <summary>The driver takes back an offer the rider has not answered.</summary>
    Task<BaseResponse> Withdraw(int id);

    /// <summary>The offers on the caller's own schedule, and the driver it has, if any.</summary>
    Task<BaseResponse<List<SeriesRow>>> OffersFor(int scheduleId);

    /// <summary>The schedule owner accepts one offer: the driver takes every upcoming day it covers.</summary>
    Task<BaseResponse<SeriesResult>> Accept(int id);

    Task<BaseResponse> Decline(int id);

    // ── Rider joins a driver's series ──

    /// <summary>A rider books every upcoming day of the recurring trip this one belongs to.</summary>
    Task<BaseResponse<SeriesResult>> Join(int tripId, JoinSeriesInput input);

    // ── Either side ──

    /// <summary>Every live commitment the caller is on, from either side, and the recently ended.</summary>
    Task<BaseResponse<List<SeriesRow>>> Mine();

    Task<BaseResponse<SeriesRow>> Get(int id);

    Task<BaseResponse<SeriesEndPreview>> EndPreview(int id);

    Task<BaseResponse<SeriesRow>> End(int id, EndSeriesInput input);

    // ── Hooks and clocks ──

    /// <summary>The materialiser wrote a day of a driver's schedule: book the riders on its series.</summary>
    Task OnTripGenerated(Trip trip, TripSchedule schedule);

    /// <summary>The materialiser wrote a day of a rider's schedule: give it to the series driver.</summary>
    Task OnRequestGenerated(RideRequest request, TripSchedule schedule);

    /// <summary>A schedule was deleted: every commitment on it ends, at nobody's cost.</summary>
    Task OnScheduleDeleted(TripSchedule schedule);

    /// <summary>Offers the rider left unanswered past their deadline are decided.</summary>
    Task<int> DecideDue();

    /// <summary>Commitments whose last day has passed are closed.</summary>
    Task<int> CloseFinished();

    /// <summary>The week-ahead message, once a week, on the configured day.</summary>
    Task<int> SendWeeklySummaries();

    // ── Admin ──

    Task<BaseResponse<PageOutput<SeriesRow>>> List(PageInput page, SeriesStatus? status, SeriesSide? side);

    /// <summary>Ends a commitment at once, at nobody's cost.</summary>
    Task<BaseResponse<SeriesRow>> AdminEnd(int id);
}

/// <summary>
/// Adds the recurrence behind a row — "Repeats Sun–Thu" — to the lists the
/// clients draw cards from. Kept apart from <see cref="ISeriesService"/> so
/// the booking and trip services it decorates can use it without a cycle.
/// </summary>
[TransientInjectable]
public interface ISeriesInfoService
{
    Task Decorate(IReadOnlyCollection<RideRequestRow> rows);
    Task Decorate(IReadOnlyCollection<TripOutput> rows);
    Task Decorate(IReadOnlyCollection<BookingOutput> rows);
}

/// <summary>Decorates a response on its way out, for the controllers.</summary>
public static class SeriesInfoExtensions
{
    public static async Task<BaseResponse<T>> With<T>(this ISeriesInfoService info, BaseResponse<T> response)
    {
        switch (response.Data)
        {
            case RideRequestRow row: await info.Decorate([row]); break;
            case List<RideRequestRow> rows: await info.Decorate(rows); break;
            case TripOutput trip: await info.Decorate([trip]); break;
            case List<TripOutput> trips: await info.Decorate(trips); break;
            case BookingOutput booking: await info.Decorate([booking]); break;
            case List<BookingOutput> bookings: await info.Decorate(bookings); break;
            case Search.Models.DemandSearchResult demand:
                await info.Decorate(demand.Matches.Select(m => m.Request).ToList());
                break;
            case Search.Models.SearchResult search:
                await info.Decorate(search.Matches.Select(m => m.Trip).ToList());
                break;
        }
        return response;
    }
}
