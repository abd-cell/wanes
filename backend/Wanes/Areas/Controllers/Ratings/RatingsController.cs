using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Ratings;
using Wanes.Areas.Services.Ratings.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Ratings;

[AppAuthorize]
public class RatingsController : BaseApiController
{
    private readonly IRatingService ratingService;

    public RatingsController(IRatingService ratingService) => this.ratingService = ratingService;

    [HttpPost]
    public async Task<BaseResponse> Rate([FromBody] CreateRatingInput input)
        => await ratingService.Rate(input);
}
