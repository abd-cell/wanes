using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Places;
using Wanes.Areas.Services.Users.Places.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

[AppAuthorize]
[Route("api/v1/me/places")]
public class SavedPlacesController : BaseApiController
{
    private readonly ISavedPlaceService savedPlaceService;

    public SavedPlacesController(ISavedPlaceService savedPlaceService)
        => this.savedPlaceService = savedPlaceService;

    [HttpGet]
    public async Task<BaseResponse<List<SavedPlaceOutput>>> List() => await savedPlaceService.GetUserPlaces();

    [HttpPost]
    public async Task<BaseResponse<SavedPlaceOutput>> Add([FromBody] SavedPlaceInput input)
        => await savedPlaceService.Create(input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await savedPlaceService.Delete(id);
}
