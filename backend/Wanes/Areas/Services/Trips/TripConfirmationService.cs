using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Trips.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Trips;

public class TripConfirmationService : ITripConfirmationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<Booking> bookingRepository;

    public TripConfirmationService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IAppConfigurationService appConfigurationService,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.appConfigurationService = appConfigurationService;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.bookingRepository = bookingRepository;
    }

    public IReadOnlyList<int> ApplyThreshold(Trip trip, IReadOnlyList<Booking> bookings)
    {
        if (trip.MinSeatsToConfirm <= TripConfirmationRules.NoThreshold) return [];

        var held = TripConfirmationRules.HeldSeats(bookings);
        if (!TripConfirmationRules.IsMet(trip.MinSeatsToConfirm, held)) return [];

        return Commit(trip, bookings);
    }

    public async Task<BaseResponse<TripOutput>> ConfirmNow(int tripId)
    {
        var driverId = securityManager.RequireUserId();

        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);

        var bookings = await SeatsOf(tripId);
        if (!IsDecidable(trip, bookings))
            return new BaseResponse<TripOutput>(default, ErrorCode.TripNotConfirmable);

        // Running with fewer seats than they asked for is the driver dropping
        // the condition, not meeting it — and it lands in exactly the same place,
        // because the stamp is what confirmation *is*. The trip keeps the
        // threshold it was posted with, as a record of what was asked for;
        // nothing re-derives it as gathering afterwards, so nobody is asked twice.
        var confirmed = Commit(trip, bookings);
        tripRepository.Update(trip);

        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripConfirm, nameof(Trip), trip.Id);
        await NotifyConfirmed(trip, confirmed);

        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver));
    }

    public async Task<BaseResponse<TripOutput>> CancelForLowSeats(int tripId)
    {
        var driverId = securityManager.RequireUserId();

        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);

        var bookings = await SeatsOf(tripId);
        if (!IsDecidable(trip, bookings))
            return new BaseResponse<TripOutput>(default, ErrorCode.TripNotConfirmable);

        var riders = await CallOff(trip, bookings, driverId);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripCancelLowSeats, nameof(Trip), trip.Id);
        await NotifyCalledOff(trip, riders);

        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver));
    }

    public async Task<int> PromptDue()
    {
        var settings = await appConfigurationService.Get();
        var cutoff = settings.Data?.ConfirmCutoffMinutes ?? TripConfirmationRules.DefaultCutoffMinutes;
        var lead = settings.Data?.ConfirmDecisionLeadMinutes
                   ?? TripConfirmationRules.DefaultDecisionLeadMinutes;

        var now = DateTime.UtcNow;

        // The window opens at (departure − cutoff − lead) and the deadline is at
        // (departure − cutoff). Expressed as a bound on DepartAt so the database
        // can use the (Status, DepartAt) index instead of computing per row.
        var promptFrom = now + TripConfirmationRules.Cutoff(cutoff) + TripConfirmationRules.DecisionLead(lead);
        var promptTo = now + TripConfirmationRules.Cutoff(cutoff);

        var candidates = await tripRepository
            .Where(t => (t.Status == TripStatus.Posted || t.Status == TripStatus.Full)
                        && t.MinSeatsToConfirm > TripConfirmationRules.NoThreshold
                        && t.ConfirmPromptedAt == null
                        && t.DepartAt > promptTo
                        && t.DepartAt <= promptFrom)
            .ToListAsync();
        if (candidates.Count == 0) return 0;

        var gathering = await StillGathering(candidates);
        if (gathering.Count == 0) return 0;

        // Stamped and saved before the push, for the same reason the posting
        // sweeper stamps first: delivery is best-effort, the stamp has to be
        // exactly once, and a driver asked four times in a quarter of an hour
        // learns to ignore the one notification that needed an answer.
        foreach (var (trip, _) in gathering)
        {
            trip.ConfirmPromptedAt = now;
            tripRepository.Update(trip);
        }
        await unitOfWork.SaveAsync();

        // Only a driven trip can have a seat threshold to decide on: a trip
        // still waiting for a driver has no threshold and nobody to ask.
        foreach (var (trip, held) in gathering.Where(g => g.Trip.DriverId != null))
            await notificationService.Notify(trip.DriverId!.Value,
                NotificationTemplate.TripConfirmDecisionDriver,
                args: new
                {
                    seats = held,
                    min = trip.MinSeatsToConfirm,
                    origin = trip.OriginAddress,
                    destination = trip.DestinationAddress,
                },
                data: new { tripId = trip.Id, seatsHeld = held, minSeats = trip.MinSeatsToConfirm });

        return gathering.Count;
    }

    public async Task<int> ResolveDue()
    {
        var settings = await appConfigurationService.Get();
        var cutoff = TripConfirmationRules.Cutoff(
            settings.Data?.ConfirmCutoffMinutes ?? TripConfirmationRules.DefaultCutoffMinutes);

        var deadline = DateTime.UtcNow + cutoff;

        var candidates = await tripRepository
            .Where(t => (t.Status == TripStatus.Posted || t.Status == TripStatus.Full)
                        && t.MinSeatsToConfirm > TripConfirmationRules.NoThreshold
                        && t.DepartAt <= deadline)
            .ToListAsync();
        if (candidates.Count == 0) return 0;

        var gathering = await StillGathering(candidates);
        if (gathering.Count == 0) return 0;

        var calledOff = new List<(Trip Trip, List<int> Riders)>();
        foreach (var (trip, _) in gathering)
        {
            var bookings = await SeatsOf(trip.Id);

            // No actor: the sweeper runs outside any request, so the history row
            // and the audit entry record what happened with nobody's name on it.
            var riders = await CallOff(trip, bookings, changedBy: null);
            calledOff.Add((trip, riders));
        }
        await unitOfWork.SaveAsync();

        foreach (var (trip, riders) in calledOff)
        {
            await auditService.LogAsync(AuditActions.TripCancelLowSeats, nameof(Trip), trip.Id);
            await NotifyCalledOff(trip, riders);
        }

        return calledOff.Count;
    }

    /// <summary>
    /// Flips every pending seat to confirmed and returns their riders. The one
    /// place that transition happens, so the threshold being met and the driver
    /// waiving it cannot end up meaning different things.
    /// </summary>
    private IReadOnlyList<int> Commit(Trip trip, IReadOnlyList<Booking> bookings)
    {
        // Already stamped: a retried confirm, or two bookings that crossed the
        // threshold together. Confirm nobody again and — the half that matters —
        // return nobody to notify. Telling three riders twice that their ride is
        // on is exactly the duplicate a threshold race produces.
        if (trip.IsConfirmed) return [];

        var pending = bookings.Where(b => BookingStatusRules.IsPending(b.Status)).ToList();

        foreach (var booking in pending)
        {
            booking.Status = BookingStatus.Confirmed;
            bookingRepository.Update(booking);
        }

        // The stamp, in the same commit as the seats it commits. Trip.Status is
        // deliberately untouched: confirmation is its own dimension now, and
        // Pending and Confirmed are both live so the lifecycle derivation would
        // answer the same either way.
        trip.ConfirmedAt = DateTime.UtcNow;
        tripRepository.Update(trip);

        return pending.Select(b => b.RiderId).Distinct().ToList();
    }

    /// <summary>
    /// Cancels the trip and every seat still held on it, returning the riders to
    /// tell. Shared by the driver's own call-off and the sweeper's, which is why
    /// <paramref name="changedBy"/> is nullable.
    /// </summary>
    private async Task<List<int>> CallOff(Trip trip, IReadOnlyList<Booking> bookings, int? changedBy)
    {
        var live = bookings.Where(b => BookingStatusRules.IsLive(b.Status)).ToList();
        foreach (var booking in live)
        {
            booking.Status = BookingStatus.Cancelled;
            bookingRepository.Update(booking);
        }

        trip.Status = TripStatus.Cancelled;
        tripRepository.Update(trip);

        tripHistoryRepository.Create(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = TripStatus.Cancelled,
            ChangedBy = changedBy,
        });

        await Task.CompletedTask;
        return live.Select(b => b.RiderId).Distinct().ToList();
    }

    /// <summary>
    /// The trips from <paramref name="candidates"/> that really are short of
    /// their threshold, with the seats they hold.
    ///
    /// The seat count cannot come from the query — held seats are a sum over
    /// live bookings, and "live" is
    /// <see cref="BookingStatusRules.IsLive"/>'s business, not SQL's. One
    /// grouped read for the whole batch rather than one per trip.
    /// </summary>
    private async Task<List<(Trip Trip, int Held)>> StillGathering(List<Trip> candidates)
    {
        var ids = candidates.Select(t => t.Id).ToList();
        var seats = await bookingRepository
            .Where(b => ids.Contains(b.TripId))
            .Select(b => new { b.TripId, b.Seats, b.Status })
            .ToListAsync();

        var heldByTrip = seats
            .Where(s => BookingStatusRules.IsLive(s.Status))
            .GroupBy(s => s.TripId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Seats));

        return candidates
            .Select(t => (Trip: t, Held: heldByTrip.GetValueOrDefault(t.Id)))
            .Where(x => TripConfirmationRules.IsGathering(
                x.Trip.Status, x.Trip.MinSeatsToConfirm, x.Held, x.Trip.ConfirmedAt))
            .ToList();
    }

    /// <summary>
    /// Whether there is a decision left to make: a threshold, unmet, on a trip
    /// that has not left or been called off.
    /// </summary>
    private static bool IsDecidable(Trip trip, IReadOnlyList<Booking> bookings) =>
        TripConfirmationRules.IsGathering(
            trip.Status, trip.MinSeatsToConfirm, TripConfirmationRules.HeldSeats(bookings),
            trip.ConfirmedAt);

    private async Task<List<Booking>> SeatsOf(int tripId) =>
        await bookingRepository.Where(b => b.TripId == tripId).ToListAsync();

    private async Task NotifyConfirmed(Trip trip, IReadOnlyList<int> riderIds)
    {
        if (riderIds.Count == 0) return;
        await notificationService.NotifyMany(riderIds, NotificationTemplate.TripConfirmedRider,
            args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
            data: new { tripId = trip.Id });
    }

    private async Task NotifyCalledOff(Trip trip, IReadOnlyList<int> riderIds)
    {
        if (riderIds.Count == 0) return;
        await notificationService.NotifyMany(riderIds, NotificationTemplate.TripLowSeatsCancelledRider,
            args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
            data: new { tripId = trip.Id });
    }
}
