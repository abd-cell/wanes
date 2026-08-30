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
}
