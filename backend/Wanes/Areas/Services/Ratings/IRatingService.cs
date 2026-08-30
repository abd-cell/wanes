using Wanes.Areas.Services.Ratings.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Ratings;

[TransientInjectable]
public interface IRatingService
{
    Task<BaseResponse> Rate(CreateRatingInput input);
}
