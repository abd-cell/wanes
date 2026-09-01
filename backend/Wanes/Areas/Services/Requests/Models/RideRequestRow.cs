using Wanes.Areas.Domain.Requests;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Requests.Models;

public class RideRequestRow
{
    public int Id { get; set; }
    public int RiderId { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public int Seats { get; set; }
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// The departure the rider searched for. A driver deciding whether to take
    /// the hail is agreeing to this time, not to leaving now, so the card has to
    /// say it — and it is what the trip created on Accept departs at.
    /// </summary>
    public DateTime WantedDepartAt { get; set; }

    /// <summary>
    /// When the hail stops being answerable. The clients draw a countdown from
    /// this rather than adding the configured TTL to <see cref="RequestedAt"/>
    /// themselves: the window is admin-set and can change while a request is
    /// already open, and only the value stamped on the row is the real deadline.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public RideRequestStatus Status { get; set; }
    public int? MatchedTripId { get; set; }

    public RideRequestRow() { }

    public RideRequestRow(RideRequest request)
    {
        if (request == null) return;

        Id = request.Id;
        RiderId = request.RiderId;
        OriginAddress = request.OriginAddress;
        OriginLat = request.Origin.Y;
        OriginLng = request.Origin.X;
        DestinationAddress = request.DestinationAddress;
        DestinationLat = request.Destination.Y;
        DestinationLng = request.Destination.X;
        Seats = request.Seats;
        RequestedAt = request.RequestedAt;
        WantedDepartAt = request.WantedDepartAt;
        ExpiresAt = request.ExpiresAt;
        Status = request.Status;
        MatchedTripId = request.MatchedTripId;
    }
}
