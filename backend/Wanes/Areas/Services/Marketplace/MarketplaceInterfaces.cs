using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Marketplace;

[TransientInjectable]
public interface IAcknowledgementService
{
    Task<BaseResponse<List<AcknowledgementOutput>>> Mine();

    /// <summary>Records the caller's agreement. Agreeing twice is a success, not a second row.</summary>
    Task<BaseResponse<AcknowledgementOutput>> Record(AcknowledgementInput input);
}

[TransientInjectable]
public interface IDemandAlertService
{
    Task<BaseResponse<List<DemandAlertOutput>>> Mine();
    Task<BaseResponse<DemandAlertOutput>> Create(DemandAlertInput input);
    Task<BaseResponse> Delete(int id);

    /// <summary>
    /// Tells every driver whose alert this request now satisfies — once per
    /// alert per request. Best-effort; returns the number of drivers told.
    /// </summary>
    Task<int> Match(RideRequest request);

    Task<BaseResponse<PageOutput<DemandAlertOutput>>> List(PageInput page, int? driverId);
}

[TransientInjectable]
public interface IReliabilityService
{
    /// <summary>What cancelling this trip now would cost the driver.</summary>
    Task<CancelPreviewOutput> PreviewDriverCancel(Trip trip, int ridersAffected, bool ridersRequeued);

    /// <summary>
    /// Writes the cancellation to the driver's record, updates their counters,
    /// and warns or pauses them as the thresholds say. Call after the cancel
    /// has committed.
    /// </summary>
    Task<ReliabilityEvent> RecordDriverCancel(Trip trip, int driverId, int ridersAffected,
        CancelReason? reason, string? note, TripStatus statusAtCancel);

    /// <summary>Records a rider giving a seat back close to departure. Does nothing otherwise.</summary>
    Task RecordRiderCancel(Booking booking, Trip trip);

    Task RecordRiderNoShow(Booking booking, Trip trip);

    /// <summary>Null when the driver may take work leaving at <paramref name="departAt"/>.</summary>
    ErrorCode? CheckCanTake(User driver, DateTime departAt, DateTime now);

    Task<BaseResponse<ReliabilityOutput>> Mine();

    Task<BaseResponse<PageOutput<ReliabilityEventRow>>> List(PageInput page, int? userId, bool? needsReview);

    Task<BaseResponse<ReliabilityEventRow>> Get(int id);

    /// <summary>Takes an entry off the scales (a breakdown, a safety call) without deleting it.</summary>
    Task<BaseResponse<ReliabilityEventRow>> Waive(int id, WaiveInput input);
}

[TransientInjectable]
public interface IDemandRecoveryService
{
    /// <summary>
    /// When a driver cancels a trip formed from a request, puts its riders
    /// back on the market in a new request. Returns the riders re-queued —
    /// they get a "finding you another driver" message instead of a plain
    /// cancellation.
    /// </summary>
    Task<List<int>> ReopenFor(Trip trip, IReadOnlyCollection<Booking> cancelled);

    /// <summary>Whether <see cref="ReopenFor"/> would re-queue anybody for this trip.</summary>
    Task<bool> WouldReopen(Trip trip);
}
