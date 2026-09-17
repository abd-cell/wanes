using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Series;
using Wanes.Areas.Services.Series.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Series;

/// <summary>
/// Whole-series commitments. The starting points live on the thing being
/// committed to — a recurring request's card, a recurring trip — and
/// everything after is on the commitment itself.
/// </summary>
[AppAuthorize]
[Route("api/v1/series")]
public class SeriesController : BaseApiController
{
    private readonly ISeriesService service;

    public SeriesController(ISeriesService service) => this.service = service;

    /// <summary>A driver offers to drive every day of the recurring request this card is a day of.</summary>
    [HttpPost("ride-requests/{rideRequestId:int}")]
    public async Task<BaseResponse<SeriesRow>> Propose(int rideRequestId, [FromBody] ProposeSeriesInput input)
        => await service.Propose(rideRequestId, input);

    /// <summary>A rider books every upcoming day of the recurring trip this one is a day of.</summary>
    [HttpPost("trips/{tripId:int}")]
    public async Task<BaseResponse<SeriesResult>> Join(int tripId, [FromBody] JoinSeriesInput input)
        => await service.Join(tripId, input);

    /// <summary>The driver offers and the driver in place on the caller's own schedule.</summary>
    [HttpGet("schedules/{scheduleId:int}")]
    public async Task<BaseResponse<List<SeriesRow>>> OffersFor(int scheduleId)
        => await service.OffersFor(scheduleId);

    [HttpGet("mine")]
    public async Task<BaseResponse<List<SeriesRow>>> Mine() => await service.Mine();

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<SeriesRow>> Get(int id) => await service.Get(id);

    [HttpPost("{id:int}/accept")]
    public async Task<BaseResponse<SeriesResult>> Accept(int id) => await service.Accept(id);

    [HttpPost("{id:int}/decline")]
    public async Task<BaseResponse> Decline(int id) => await service.Decline(id);

    /// <summary>The driver takes back an offer the rider has not answered.</summary>
    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Withdraw(int id) => await service.Withdraw(id);

    [HttpGet("{id:int}/end-preview")]
    public async Task<BaseResponse<SeriesEndPreview>> EndPreview(int id) => await service.EndPreview(id);

    [HttpPost("{id:int}/end")]
    public async Task<BaseResponse<SeriesRow>> End(int id, [FromBody] EndSeriesInput? input = null)
        => await service.End(id, input ?? new EndSeriesInput());
}

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/series")]
public class AdminSeriesController : BaseApiController
{
    private readonly ISeriesService service;

    public AdminSeriesController(ISeriesService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<SeriesRow>>> List(
        [FromQuery] PageInput page, [FromQuery] SeriesStatus? status, [FromQuery] SeriesSide? side)
        => await service.List(page, status, side);

    [HttpPost("{id:int}/end")]
    public async Task<BaseResponse<SeriesRow>> End(int id) => await service.AdminEnd(id);

    /// <summary>The console's generic grid removes rows with DELETE; a series is ended, never deleted.</summary>
    [HttpDelete("{id:int}")]
    public async Task<BaseResponse<SeriesRow>> Close(int id) => await service.AdminEnd(id);
}
