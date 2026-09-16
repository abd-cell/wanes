using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Schedules;
using Wanes.Areas.Services.Schedules.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Schedules;

/// <summary>
/// Recurring postings, from either side. One controller because it is one
/// resource — which kind of row a schedule generates is a field on it, not a
/// different endpoint.
/// </summary>
[AppAuthorize]
[Route("api/v1/schedules")]
public class TripSchedulesController : BaseApiController
{
    private readonly ITripScheduleService tripScheduleService;

    public TripSchedulesController(ITripScheduleService tripScheduleService)
        => this.tripScheduleService = tripScheduleService;

    [HttpPost]
    public async Task<BaseResponse<TripScheduleRow>> Create([FromBody] TripScheduleInput input)
        => await tripScheduleService.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<TripScheduleRow>> Update(int id, [FromBody] TripScheduleInput input)
        => await tripScheduleService.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id)
        => await tripScheduleService.Delete(id);

    [HttpGet("mine")]
    public async Task<BaseResponse<List<TripScheduleRow>>> Mine()
        => await tripScheduleService.GetUserSchedules();

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<TripScheduleRow>> Get(int id)
        => await tripScheduleService.Get(id);
}
