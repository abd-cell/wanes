using Wanes.Areas.Services.Schedules.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Schedules;

/// <summary>
/// Recurring postings, and the pass that turns them into real rows.
///
/// The whole point of the entity is that nothing else knows it exists: search,
/// booking, claiming and the availability rules only ever see the ordinary
/// trips and postings this produces. So the interface is a plain CRUD plus one
/// generator, and the generator is the only interesting method.
/// </summary>
[TransientInjectable]
public interface ITripScheduleService
{
    Task<BaseResponse<TripScheduleRow>> Create(TripScheduleInput input);

    /// <summary>
    /// Edits the recurrence. Occurrences already generated are left alone —
    /// somebody may have booked one — so a change takes effect from the next
    /// date the materialiser has not yet reached.
    /// </summary>
    Task<BaseResponse<TripScheduleRow>> Update(int id, TripScheduleInput input);

    /// <summary>
    /// Deletes the schedule and cancels its future occurrences that nobody has
    /// taken a seat on. Booked ones stand and run.
    /// </summary>
    Task<BaseResponse> Delete(int id);

    Task<BaseResponse<List<TripScheduleRow>>> GetUserSchedules();

    Task<BaseResponse<TripScheduleRow>> Get(int id);

    /// <summary>
    /// Generates the occurrences now inside the horizon, for every live
    /// schedule. Returns how many rows were written.
    ///
    /// Idempotent: each occurrence is keyed by (schedule, date) with a unique
    /// index behind it, so a pass that runs twice — or catches up after an
    /// outage — produces one row per morning either way.
    /// </summary>
    Task<int> MaterialiseDue();
}
