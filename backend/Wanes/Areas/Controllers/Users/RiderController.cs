using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Areas.Services.Users.Availability.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

[AppAuthorize]
[Route("api/v1/me/rider")]
public class RiderController : BaseApiController
{
    private readonly IRiderAvailabilityService riderAvailabilityService;

    public RiderController(IRiderAvailabilityService riderAvailabilityService)
    {
        this.riderAvailabilityService = riderAvailabilityService;
    }

    /// <summary>
    /// The departures the caller already holds a seat at, so search can grey out
    /// the times it knows a booking would be refused instead of letting the
    /// rider pick one, search, and read the refusal at the tap that mattered.
    /// </summary>
    [HttpGet("availability")]
    public async Task<BaseResponse<RiderAvailabilityOutput>> Availability([FromQuery] int? ignoreTripId)
        => await riderAvailabilityService.GetMySchedule(ignoreTripId);
}
