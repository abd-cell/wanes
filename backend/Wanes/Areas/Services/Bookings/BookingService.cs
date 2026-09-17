using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Marketplace;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Users.Availability;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Bookings;

public class BookingService : IBookingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly ITripConfirmationService tripConfirmationService;
    private readonly IRiderAvailabilityService riderAvailabilityService;
    private readonly IReliabilityService reliabilityService;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<User> userRepository;

    public BookingService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        ITripConfirmationService tripConfirmationService,
        IRiderAvailabilityService riderAvailabilityService,
        IReliabilityService reliabilityService,
        IRepository<Booking> bookingRepository,
        IRepository<Trip> tripRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.tripConfirmationService = tripConfirmationService;
        this.riderAvailabilityService = riderAvailabilityService;
        this.reliabilityService = reliabilityService;
        this.bookingRepository = bookingRepository;
        this.tripRepository = tripRepository;
        this.userRepository = userRepository;
    }

    /// <summary>
    /// Joins a trip. No driver approval and no second handshake: the rider read
    /// the price before they tapped, so the seat is Confirmed the moment it
    /// exists and the seats come off the trip in the same commit.
    ///
    /// Except on a trip still short of the seats its driver asked for. There the
    /// booking lands Pending — held, but nobody committed — and the seat that
    /// finally meets the threshold commits everybody at once.
    ///
    /// Wrapped in a retry because taking the last seat is a race. The seat
    /// decrement is guarded by the trip's row version, so a rider who lost it
    /// changes no rows and lands here rather than overselling — and losing is
    /// not the same as being refused: on a four-seat trip both riders should get
    /// a seat, and the loser only has to read again. The refusal, when there is
    /// one, comes out of the ordinary checks on the fresh read.
    /// </summary>
    public Task<BaseResponse<BookingOutput>> Create(CreateBookingInput input) =>
        CreateWithRetry(securityManager.RequireUserId(), input, null);

    public Task<BaseResponse<BookingOutput>> CreateForSeries(int riderId, CreateBookingInput input,
        int seriesCommitmentId) =>
        CreateWithRetry(riderId, input, seriesCommitmentId);

    private async Task<BaseResponse<BookingOutput>> CreateWithRetry(int riderId, CreateBookingInput input,
        int? seriesCommitmentId)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CreateOnce(riderId, input, seriesCommitmentId);
            }
            catch (DbUpdateConcurrencyException)
            {
                // CreateOnce has already rolled back. Its trip is still tracked
                // with the version that lost, so drop it before reading again.
                unitOfWork.Detach();
                if (attempt >= ConcurrencyRules.MaxAttempts)
                    return new BaseResponse<BookingOutput>(default, ErrorCode.Conflict);
            }
        }
    }

    private async Task<BaseResponse<BookingOutput>> CreateOnce(int riderId, CreateBookingInput input,
        int? seriesCommitmentId)
    {
        var quiet = seriesCommitmentId != null;
        var seats = input.Seats < 1 ? 1 : input.Seats;

        var rider = await userRepository.GetByIdAsync(riderId);
        if (rider == null) return new BaseResponse<BookingOutput>(default, ErrorCode.NotFound);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var trip = tripRepository.FirstOrDefault(t => t.Id == input.TripId,
                query => query.Include(t => t.Driver));
            if (trip == null)
                return await RollBack<BookingOutput>(ErrorCode.TripNotFound);
            if (trip.DriverId == riderId)
                return await RollBack<BookingOutput>(ErrorCode.CannotBookOwnTrip);
            if (trip.Status != TripStatus.Posted)
                return await RollBack<BookingOutput>(ErrorCode.TripNotBookable);
            if (trip.SeatsLeft < seats)
                return await RollBack<BookingOutput>(ErrorCode.NoSeatsLeft);

            // Both sides' conditions, checked here as well as in search. Search
            // filters so a rider is not shown a trip they cannot take; this
            // refuses, because a filtered list is a convenience and an API that
            // trusted it would let a stale screen book past the rule.
            if (RiderEligibilityRules.CheckRider(rider, trip.Conditions, trip.DepartAt) is { } refusal)
                return await RollBack<BookingOutput>(refusal);

            // No second check on the driver. The rider's condition on who may
            // drive them is a search filter now rather than an account setting,
            // and a filter cannot be booked past: they are taking a seat with a
            // driver they just picked off a list by name. What is re-checked
            // here is the condition that belongs to somebody else — the
            // driver's, written on the trip — because that one the rider's
            // screen has no business trusting.

            // A seat this rider already holds on this trip. Two different
            // things arrive here and they deserve different answers:
            //
            //  - **the same seats again** is a retry — a double tap, or a
            //    client re-sending a request whose response it never saw. It is
            //    answered from the booking they already have, because refusing
            //    it would tell a rider they have no seat while they are holding
            //    one (§13.2);
            //  - **different seats** is a second intention, not a repeat, and
            //    AlreadyBooked is the honest refusal: changing a booking is a
            //    different operation from making one.
            var existing = bookingRepository.FirstOrDefault(b =>
                b.TripId == trip.Id && b.RiderId == riderId && b.Status != BookingStatus.Cancelled);
            if (existing != null)
            {
                if (existing.Seats != seats)
                    return await RollBack<BookingOutput>(ErrorCode.AlreadyBooked);

                await unitOfWork.RollBackAsync();
                return new BaseResponse<BookingOutput>(new BookingOutput(existing, trip));
            }

            // The same seat twice is AlreadyBooked above; this is two *different*
            // trips leaving at the same moment. Search's time picker greys those
            // slots out, but a filtered screen is a convenience — the rule has to
            // live here or a stale one books straight past it.
            if (await riderAvailabilityService.CheckCanRide(riderId, trip.DepartAt) is { } busy)
                return await RollBack<BookingOutput>(busy);

            // Reserve the seats. This reads as a plain decrement, and is safe
            // only because Trip carries a row version: the UPDATE EF writes is
            // conditional on the value loaded above, so two riders cannot both
            // take the same last seat. Without it the transaction would happily
            // let both through — READ COMMITTED does not block either read.
            //
            // Pending seats decrement too. A held seat is not for sale twice,
            // whoever is still to answer for it.
            trip.SeatsLeft -= seats;
            tripRepository.Update(trip);

            var siblings = await bookingRepository.Where(b => b.TripId == trip.Id).ToListAsync();

            // Where the seat lands. A trip with no threshold commits on the
            // spot — the rider read the price before they tapped. One still
            // short of its threshold holds the seat without committing anybody,
            // and carries no hold deadline: that trip's confirm cutoff is the
            // clock, and a second one would let the two disagree about which
            // killed the seat.
            var heldAfter = TripConfirmationRules.HeldSeats(siblings) + seats;
            var meetsThreshold = TripConfirmationRules.IsMet(trip.MinSeatsToConfirm, heldAfter);

            var booking = new Booking
            {
                TripId = trip.Id,
                RiderId = riderId,
                Seats = seats,
                Status = meetsThreshold ? BookingStatus.Confirmed : BookingStatus.Pending,
                BoardingCode = BoardingCodes.New(),
                SharedTermsAcceptedAt = input.AcceptSharedRide == true ? DateTime.UtcNow : null,
                SeriesCommitmentId = seriesCommitmentId,
            };
            bookingRepository.Create(booking);

            // If this is the seat that made the trip, everybody who was waiting
            // on it is committed too. The list comes back empty when there was
            // no threshold, or when it was already met.
            var confirmed = meetsThreshold
                ? tripConfirmationService.ApplyThreshold(trip, [.. siblings, booking])
                : [];

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.BookingConfirm, nameof(Booking), booking.Id);

            // Both sides care: the rider gets their receipt, the driver learns a
            // seat just went. Sent after the commit so a push can't outrun the row.
            // A series seat is announced once for the series, not once a day.
            if (!quiet)
            {
                await notificationService.Notify(riderId, NotificationTemplate.BookingConfirmedRider,
                    args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
                    data:
                    new { bookingId = booking.Id, tripId = trip.Id });

                // Status Posted was checked above, and only a trip somebody is
                // driving can be Posted — a driverless one is AwaitingDriver.
                await notificationService.Notify(trip.DriverId!.Value,
                    NotificationTemplate.BookingConfirmedDriver,
                    args: new { seats, seatsLeft = trip.SeatsLeft },
                    data:
                    new { bookingId = booking.Id, tripId = trip.Id });
            }

            // The threshold being met is news to everybody who was waiting on
            // it, this rider included: their seat went from held to theirs
            // because somebody else booked.
            if (confirmed.Count > 0)
                await notificationService.NotifyMany(confirmed, NotificationTemplate.TripConfirmedRider,
                    args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
                    data: new { tripId = trip.Id });

            return new BaseResponse<BookingOutput>(new BookingOutput(booking, trip));
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    /// <summary>
    /// Gives a seat back — the rider leaving.
    ///
    /// The only way off a trip, and deliberately the only one: a rider who does
    /// not like the price a driver named leaves exactly as a rider who changed
    /// their mind does. Two verbs for one act would have meant two ways of
    /// returning the seats.
    ///
    /// Retried on the same terms as <see cref="Create"/>: returning a seat
    /// writes the same trip row a rider taking one does, so the two contend, and
    /// the loser has only to read the trip again.
    /// </summary>
    public async Task<BaseResponse> Cancel(int id)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CancelOnce(id);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.Detach();
                if (attempt >= ConcurrencyRules.MaxAttempts)
                    return new BaseResponse(ErrorCode.Conflict);
            }
        }
    }

    private async Task<BaseResponse> CancelOnce(int id)
    {
        var riderId = securityManager.RequireUserId();

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var booking = bookingRepository.FirstOrDefault(b => b.Id == id && b.RiderId == riderId);
            if (booking == null)
            {
                await unitOfWork.RollBackAsync();
                return new BaseResponse(ErrorCode.BookingNotFound);
            }
            // Already cancelled is a **success**: the rider asked for the seat
            // to be gone and it is gone, and a retry of a cancel that worked
            // must not read as a failure (§13.2). Completed is different — the
            // ride happened, and there is nothing to undo.
            if (booking.Status == BookingStatus.Cancelled)
            {
                await unitOfWork.RollBackAsync();
                return new BaseResponse();
            }
            if (booking.Status == BookingStatus.Completed)
            {
                await unitOfWork.RollBackAsync();
                return new BaseResponse(ErrorCode.Conflict);
            }

            var trip = await tripRepository.GetByIdAsync(booking.TripId);

            booking.Status = BookingStatus.Cancelled;
            bookingRepository.Update(booking);

            if (trip != null) ReturnSeats(trip, booking);

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.BookingCancel, nameof(Booking), booking.Id);

            // Giving a seat back late leaves a driver short with no time to
            // refill it — recorded, as a driver's late cancellation is.
            if (trip?.DriverId != null) await reliabilityService.RecordRiderCancel(booking, trip);

            // The driver is the one who needs to know a seat came back.
            // Nobody to tell when nobody is driving it yet: the rider simply
            // left a trip that is still looking for a driver.
            if (trip?.DriverId is { } driverToTell)
                await notificationService.Notify(driverToTell,
                    NotificationTemplate.BookingCancelledDriver,
                    args: new
                    {
                        seats = booking.Seats,
                        origin = trip.OriginAddress,
                        destination = trip.DestinationAddress,
                    },
                    data:
                    new { bookingId = booking.Id, tripId = trip.Id });

            return new BaseResponse();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    public async Task<BaseResponse<List<BookingOutput>>> GetUserBookings()
    {
        var riderId = securityManager.RequireUserId();

        var bookings = await bookingRepository
            .Where(b => b.RiderId == riderId,
                query => query.Include(b => b.Trip).ThenInclude(t => t!.Driver))
            .OrderByDescending(b => b.Id)
            .ToListAsync();

        var data = bookings.Select(b => new BookingOutput(b, b.Trip)).ToList();
        return new BaseResponse<List<BookingOutput>>(data);
    }

    /// <summary>
    /// Puts a released seat back on the trip and re-derives where the trip
    /// stands.
    ///
    /// The status is read back off the bookings rather than patched here: the
    /// seat that just went may have been the last one keeping the trip Active
    /// mid-journey — and only
    /// <see cref="TripStatusRules.Derive"/> knows the difference.
    /// </summary>
    private void ReturnSeats(Trip trip, Booking booking)
    {
        if (TripStatusRules.IsOpenForSeats(trip.Status))
            trip.SeatsLeft += booking.Seats;

        var statuses = bookingRepository
            .Where(b => b.TripId == trip.Id)
            .Select(b => new { b.Id, b.Status })
            .ToList()
            .Select(b => b.Id == booking.Id ? booking.Status : b.Status)
            .ToList();

        trip.Status = TripStatusRules.Derive(trip.Status, statuses, trip.SeatsLeft);
        tripRepository.Update(trip);
    }

    private async Task<BaseResponse<T>> RollBack<T>(ErrorCode errorCode)
    {
        await unitOfWork.RollBackAsync();
        return new BaseResponse<T>(default, errorCode);
    }
}
