using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/ratings")]
public class AdminRatingsController : BaseApiController
{
    private readonly IAdminRatingService service;

    public AdminRatingsController(IAdminRatingService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<RatingRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] RatingDirection? direction,
        [FromQuery] int? toUserId,
        [FromQuery] int? fromUserId)
        => await service.List(page, direction, toUserId, fromUserId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<RatingRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<RatingRow>> Create([FromBody] RatingInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<RatingRow>> Update(int id, [FromBody] RatingInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
