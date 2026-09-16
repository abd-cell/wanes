using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;

namespace Wanes.Tests.TestDoubles;

/// <summary>Terse entity builders for arranging tests.</summary>
public static class Build
{
    public static User Driver(int id, DriverStatus status = DriverStatus.Verified) => new()
    {
        Id = id, Phone = $"+96279000000{id}", PhoneVerified = true,
        FirstName = "Drv", IsDriver = true, DriverStatus = status,
    };

    public static User Rider(int id) => new()
    {
        Id = id, Phone = $"+96279000001{id}", PhoneVerified = true, FirstName = "Rdr", IsRider = true,
    };

    public static Vehicle Vehicle(int id, int userId, int capacity = 4) => new()
    {
        Id = id, UserId = userId, Make = "Toyota", Model = "Corolla",
        Plate = "123", SeatCapacity = capacity, IsDefault = true,
    };

    public static Trip Trip(int id, int driverId, int vehicleId, int seatsTotal = 3, TripStatus status = TripStatus.Posted)
    {
        var origin = GeoFactory.Point(31.95, 35.92);
        var dest = GeoFactory.Point(32.01, 35.87);
        return new Trip
        {
            Id = id, DriverId = driverId, VehicleId = vehicleId,
            OriginAddress = "A", Origin = origin,
            DestinationAddress = "B", Destination = dest,
            Route = GeoFactory.Line(origin, dest),
            DepartAt = DateTime.UtcNow.AddMinutes(20),
            SeatsTotal = seatsTotal, SeatsLeft = seatsTotal,
            Status = status,
        };
    }

    /// <summary>
    /// Demand: a journey riders want, with nobody driving it. The shape
    /// <c>RideRequestService.Create</c> produces, which is the only shape the
    /// rest of the rules are written against.
    /// </summary>
    public static RideRequest Request(int id, int seats = 1, DateTime? departAt = null,
        RideRequestStatus status = RideRequestStatus.Open) => new()
    {
        Id = id,
        OriginAddress = "A", Origin = GeoFactory.Point(31.95, 35.92),
        DestinationAddress = "B", Destination = GeoFactory.Point(32.01, 35.87),
        Route = GeoFactory.Line(GeoFactory.Point(31.95, 35.92), GeoFactory.Point(32.01, 35.87)),
        DepartAt = departAt ?? DateTime.UtcNow.AddHours(2),
        TimeWindowMinutes = 30,
        SeatsRequested = seats,
        RadiusMeters = 5000,
        Status = status,
    };

    /// <summary>One rider's place on a request.</summary>
    public static RideRequestParticipant Participant(int id, int requestId, int riderId,
        int seats = 1) => new()
    {
        Id = id, RideRequestId = requestId, RiderId = riderId, Seats = seats,
        Status = RideRequestParticipantStatus.Active,
    };

    /// <summary>
    /// Puts a request and its author's participation in the stores, and returns
    /// the request.
    ///
    /// Both rows, because the service reads participants through the repository
    /// rather than off a navigation property. A request seeded without one is a
    /// pool nobody is on, and the rules would rightly treat it as one.
    /// </summary>
    public static RideRequest Demand(FakeUnitOfWork uow, int id, int riderId, int seats = 1,
        DateTime? departAt = null, RideRequestStatus status = RideRequestStatus.Open)
    {
        var request = Request(id, seats, departAt, status);
        uow.Store<RideRequest>().Add(request);
        uow.Store<RideRequestParticipant>().Add(Participant(id * 100, id, riderId, seats));
        return request;
    }
}
