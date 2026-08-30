using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/places")]
public class AdminPlacesController : BaseApiController
{
    private readonly IAdminSavedPlaceService service;

    public AdminPlacesController(IAdminSavedPlaceService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<SavedPlaceRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] SavedPlaceLabel? label,
        [FromQuery] int? userId)
        => await service.List(page, label, userId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<SavedPlaceRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<SavedPlaceRow>> Create([FromBody] SavedPlaceInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<SavedPlaceRow>> Update(int id, [FromBody] SavedPlaceInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
