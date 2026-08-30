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
        Status = request.Status;
        MatchedTripId = request.MatchedTripId;
    }
}
