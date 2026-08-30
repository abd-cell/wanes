using Wanes.Areas.Services.Users.Places.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Places;

[TransientInjectable]
public interface ISavedPlaceService
{
    Task<BaseResponse<List<SavedPlaceOutput>>> GetUserPlaces();
    Task<BaseResponse<SavedPlaceOutput>> Create(SavedPlaceInput input);
    Task<BaseResponse> Delete(int id);
}
