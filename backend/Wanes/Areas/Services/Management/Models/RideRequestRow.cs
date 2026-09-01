using Wanes.Areas.Domain.Requests;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a ride request (list + detail share one shape).</summary>
public class RideRequestRow
{
    public int Id { get; set; }
    public int RiderId { get; set; }
    public string? RiderName { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestLat { get; set; }
    public double DestLng { get; set; }
    public int Seats { get; set; }
    public int RadiusMeters { get; set; }
    public RideRequestStatus Status { get; set; }
    public int? MatchedTripId { get; set; }
    public string? MatchedTripSummary { get; set; }
    public DateTime RequestedAt { get; set; }

    /// <summary>The departure the rider searched for (see the domain field).</summary>
    public DateTime WantedDepartAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime CreationDate { get; set; }

    public RideRequestRow() { }

    public RideRequestRow(RideRequest r)
    {
        Id = r.Id;
        RiderId = r.RiderId;
        OriginAddress = r.OriginAddress;
        OriginLat = r.Origin.Y;
        OriginLng = r.Origin.X;
        DestinationAddress = r.DestinationAddress;
        DestLat = r.Destination.Y;
        DestLng = r.Destination.X;
        Seats = r.Seats;
        RadiusMeters = r.RadiusMeters;
        Status = r.Status;
        MatchedTripId = r.MatchedTripId;
        RequestedAt = r.RequestedAt;
        WantedDepartAt = r.WantedDepartAt;
        ExpiresAt = r.ExpiresAt;
        CreationDate = r.CreationDate;
    }
}

/// <summary>Create/update payload for a ride request.</summary>
public class RideRequestInput
{
    public int RiderId { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestLat { get; set; }
    public double DestLng { get; set; }
    public int Seats { get; set; } = 1;
    public int RadiusMeters { get; set; } = 2000;
    public RideRequestStatus Status { get; set; } = RideRequestStatus.Open;
    public int? MatchedTripId { get; set; }
    public DateTime? WantedDepartAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
