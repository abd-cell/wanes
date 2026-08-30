using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminRatingService
{
    Task<BaseResponse<PageOutput<RatingRow>>> List(PageInput page, RatingDirection? direction, int? toUserId, int? fromUserId);
    Task<BaseResponse<RatingRow>> Get(int id);
    Task<BaseResponse<RatingRow>> Create(RatingInput input);
    Task<BaseResponse<RatingRow>> Update(int id, RatingInput input);
    Task<BaseResponse> Delete(int id);
}
