using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Trips;

/// <summary>
/// The seat threshold: everything that happens to a trip whose driver said
/// "only if it is worth it".
///
/// Its own service, and not a corner of <see cref="ITripService"/>, because
/// three unrelated callers need the same rule and would otherwise each grow
/// their own copy: a booking arriving (does this seat make the trip?), the
/// driver answering the prompt, and the sweeper resolving a trip nobody
/// answered for. <see cref="ApplyThreshold"/> is the shared half — it mutates,
/// but does not commit or notify, so each caller keeps its own transaction and
/// its own messages.
/// </summary>
[TransientInjectable]
public interface ITripConfirmationService
{
    /// <summary>
    /// Commits every held seat if the trip has reached its threshold, and
    /// returns the riders whose seat just changed — empty when there was nothing
    /// to do.
    ///
    /// Mutates the entities and nothing else: no save, no commit, no
    /// notification. The caller is inside a transaction it owns and is the only
    /// one that knows when it is safe to tell anybody.
    /// </summary>
    IReadOnlyList<int> ApplyThreshold(Trip trip, IReadOnlyList<Booking> bookings);

    /// <summary>
    /// The driver choosing to run with the seats they have, threshold unmet.
    /// Refused with <c>TripNotConfirmable</c> on a trip that has nothing to
    /// decide — already confirmed, already gone, or never had a threshold.
    /// </summary>
    Task<BaseResponse<TripOutput>> ConfirmNow(int tripId);

    /// <summary>
    /// The driver calling it off for want of riders. Every held seat cancels and
    /// its rider is told why — in the words of the empty seats, not of a driver
    /// who changed their mind about them.
    /// </summary>
    Task<BaseResponse<TripOutput>> CancelForLowSeats(int tripId);

    /// <summary>
    /// Asks the drivers whose decision is now due, once each. Returns how many
    /// were asked.
    /// </summary>
    Task<int> PromptDue();

    /// <summary>
    /// Cancels the trips whose decision deadline passed unanswered, and tells
    /// their riders. Returns how many were called off.
    ///
    /// Silence resolves to cancellation, not to running: the driver said they
    /// needed the seats, and riders left holding unconfirmed seats until
    /// departure would be stranded with no time to find another ride.
    /// </summary>
    Task<int> ResolveDue();
}
