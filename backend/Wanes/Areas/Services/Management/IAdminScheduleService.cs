using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// The console's view of recurring postings: list, read, pause or re-time, and
/// delete.
///
/// No create. A schedule is somebody's own commute, stated in their own words
/// about who they will travel with, and an admin writing one would be putting a
/// stranger in a car. What a support desk needs is the ability to stop a series
/// that is producing rides nobody wants.
/// </summary>
[TransientInjectable]
public interface IAdminScheduleService
{
    Task<BaseResponse<PageOutput<ScheduleRow>>> List(PageInput page, ActiveRole? ownerRole, int? ownerId);
    Task<BaseResponse<ScheduleRow>> Get(int id);
    Task<BaseResponse<ScheduleRow>> Update(int id, ScheduleInput input);
    Task<BaseResponse> Delete(int id);
}
