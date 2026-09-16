using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.RideRequests;

/// <summary>
/// Demand: a rider creates a request, other riders join it, and it stays open
/// until a driver is matched to it or its own departure passes.
///
/// What is deliberately **not** here is anything that makes a trip. A request
/// never becomes supply on its own — a driver offers
/// (<c>IDriverInterestService</c>), one offer is selected, and formation builds
/// the trip. Keeping that out of this service is what stops "demand" and
/// "supply" blurring back into one object.
///
/// The two sweeps at the bottom are on this interface because they are this
/// lifecycle: a request nobody answered has to reach a terminal status and leave
/// the drivers' screens, and one coming into range has to reach their phones.
/// </summary>
[TransientInjectable]
public interface IRideRequestService
{
    /// <summary>Creates demand. The author is its first participant.</summary>
    Task<BaseResponse<RideRequestRow>> Create(CreateRideRequestInput input);

    /// <summary>Every request the caller is a participant of, newest first.</summary>
    Task<BaseResponse<List<RideRequestRow>>> GetMine();

    Task<BaseResponse<RideRequestRow>> Get(int id);

    /// <summary>
    /// Joins somebody else's request. Checked both ways: the joiner against the
    /// pool's conditions, and everybody already on it against the joiner's.
    /// </summary>
    Task<BaseResponse<RideRequestRow>> Join(int id, JoinRideRequestInput input);

    /// <summary>
    /// Gives the caller's seats back. The request closes with the last of them —
    /// the rider who wrote it has no more say in that than anybody else.
    /// </summary>
    Task<BaseResponse> Leave(int id);

    /// <summary>
    /// The driver's board: open requests near them, at a departure they are free
    /// for, whose conditions they satisfy. Everything on it, an offer must be
    /// able to honour.
    /// </summary>
    Task<BaseResponse<List<RideRequestRow>>> GetNearby(double lat, double lng, int radiusMeters);

    /// <summary>
    /// Expires the requests whose departure came and went with no driver, and
    /// closes them on every connected client. Returns how many were swept.
    /// </summary>
    Task<int> ExpireDue();

    /// <summary>
    /// Pushes requests that have come within the notify horizon to nearby
    /// drivers, once each. Returns how many were pushed.
    /// </summary>
    Task<int> NotifyDue();
}
