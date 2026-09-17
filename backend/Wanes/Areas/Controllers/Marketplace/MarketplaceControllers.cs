using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Marketplace;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.Safety;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Marketplace;

/// <summary>What the caller agreed to — safety notes, shared rides.</summary>
[AppAuthorize]
[Route("api/v1/me/acknowledgements")]
public class AcknowledgementsController : BaseApiController
{
    private readonly IAcknowledgementService service;

    public AcknowledgementsController(IAcknowledgementService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<List<AcknowledgementOutput>>> Mine() => await service.Mine();

    [HttpPost]
    public async Task<BaseResponse<AcknowledgementOutput>> Record([FromBody] AcknowledgementInput input)
        => await service.Record(input);
}

/// <summary>A driver's route alerts and request watches.</summary>
[AppAuthorize]
[Route("api/v1/me/demand-alerts")]
public class DemandAlertsController : BaseApiController
{
    private readonly IDemandAlertService service;

    public DemandAlertsController(IDemandAlertService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<List<DemandAlertOutput>>> Mine() => await service.Mine();

    [HttpPost]
    public async Task<BaseResponse<DemandAlertOutput>> Create([FromBody] DemandAlertInput input)
        => await service.Create(input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}

/// <summary>The caller's own reliability record.</summary>
[AppAuthorize]
[Route("api/v1/me/reliability")]
public class ReliabilityController : BaseApiController
{
    private readonly IReliabilityService service;

    public ReliabilityController(IReliabilityService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<ReliabilityOutput>> Mine() => await service.Mine();
}

/// <summary>The emergency button, safety reports, and "follow my trip" links.</summary>
[AppAuthorize]
[Route("api/v1/safety")]
public class SafetyController : BaseApiController
{
    private readonly ISafetyService service;

    public SafetyController(ISafetyService service) => this.service = service;

    /// <summary>Raises an SOS or a report. Alerts the admin team; an SOS also messages the emergency contact.</summary>
    [HttpPost("incidents")]
    public async Task<BaseResponse<SafetyIncidentOutput>> Raise([FromBody] RaiseSafetyInput input)
        => await service.Raise(input);

    [HttpPost("bookings/{bookingId:int}/share")]
    public async Task<BaseResponse<ShareLinkOutput>> Share(int bookingId)
        => await service.CreateShareLink(bookingId);

    [HttpDelete("bookings/{bookingId:int}/share")]
    public async Task<BaseResponse> StopSharing(int bookingId)
        => await service.RevokeShareLink(bookingId);
}

/// <summary>
/// The public side of a shared trip link. Anonymous by design: the person
/// following the ride has no account. The token is the whole of the access.
/// </summary>
[Route("api/v1/share")]
public class SharedTripController : BaseApiController
{
    private readonly ISafetyService service;

    public SharedTripController(ISafetyService service) => this.service = service;

    [HttpGet("{token}")]
    public async Task<BaseResponse<SharedTripOutput>> Get(string token) => await service.GetShared(token);
}

// ── Admin ────────────────────────────────────────────────────────────────────

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/ride-requests")]
public class AdminRideRequestsController : BaseApiController
{
    private readonly IAdminRideRequestService service;

    public AdminRideRequestsController(IAdminRideRequestService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<RideRequestAdminRow>>> List(
        [FromQuery] PageInput page, [FromQuery] RideRequestStatus? status)
        => await service.List(page, status);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<RideRequestAdminRow>> Get(int id) => await service.Get(id);

    /// <summary>Closes an open request — spam, a duplicate, a request that cannot be served.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id) => await service.Cancel(id);

    /// <summary>
    /// The console's generic grid removes rows with DELETE. Demand is never
    /// deleted — it is closed, and the row stays as the record.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Close(int id) => await service.Cancel(id);
}

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/reliability")]
public class AdminReliabilityController : BaseApiController
{
    private readonly IReliabilityService service;

    public AdminReliabilityController(IReliabilityService service) => this.service = service;

    /// <param name="needsReview">1 for entries waiting on a review — a number, because the console's filters send numbers.</param>
    [HttpGet]
    public async Task<BaseResponse<PageOutput<ReliabilityEventRow>>> List(
        [FromQuery] PageInput page, [FromQuery] int? userId, [FromQuery] int? needsReview)
        => await service.List(page, userId, needsReview == 1 ? true : null);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<ReliabilityEventRow>> Get(int id) => await service.Get(id);

    [HttpPost("{id:int}/waive")]
    public async Task<BaseResponse<ReliabilityEventRow>> Waive(int id, [FromBody] WaiveInput input)
        => await service.Waive(id, input);

    /// <summary>The console's generic edit form: saving an entry with a note waives it.</summary>
    [HttpPut("{id:int}")]
    public async Task<BaseResponse<ReliabilityEventRow>> Update(int id, [FromBody] WaiveInput input)
        => await service.Waive(id, input);
}

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/safety-incidents")]
public class AdminSafetyIncidentsController : BaseApiController
{
    private readonly ISafetyService service;

    public AdminSafetyIncidentsController(ISafetyService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<SafetyIncidentOutput>>> List(
        [FromQuery] PageInput page, [FromQuery] SafetyIncidentStatus? status)
        => await service.List(page, status);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<SafetyIncidentOutput>> Get(int id) => await service.Get(id);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<SafetyIncidentOutput>> Update(int id, [FromBody] UpdateSafetyIncidentInput input)
        => await service.Update(id, input);
}

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/demand-alerts")]
public class AdminDemandAlertsController : BaseApiController
{
    private readonly IDemandAlertService service;

    public AdminDemandAlertsController(IDemandAlertService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<DemandAlertOutput>>> List(
        [FromQuery] PageInput page, [FromQuery] int? driverId)
        => await service.List(page, driverId);
}
