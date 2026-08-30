using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminSavedPlaceService
{
    Task<BaseResponse<PageOutput<SavedPlaceRow>>> List(PageInput page, SavedPlaceLabel? label, int? userId);
    Task<BaseResponse<SavedPlaceRow>> Get(int id);
    Task<BaseResponse<SavedPlaceRow>> Create(SavedPlaceInput input);
    Task<BaseResponse<SavedPlaceRow>> Update(int id, SavedPlaceInput input);
    Task<BaseResponse> Delete(int id);
}
