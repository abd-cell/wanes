using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Areas.Services.Users.Availability.Models;
using Wanes.Areas.Services.Users.Driver;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

[AppAuthorize]
[Route("api/v1/me/driver")]
public class DriverController : BaseApiController
{
    private readonly IDriverOnboardingService driverOnboardingService;
    private readonly IDriverAvailabilityService driverAvailabilityService;

    public DriverController(
        IDriverOnboardingService driverOnboardingService,
        IDriverAvailabilityService driverAvailabilityService)
    {
        this.driverOnboardingService = driverOnboardingService;
        this.driverAvailabilityService = driverAvailabilityService;
    }

    /// <summary>
    /// The departures the caller is already promised to, so a client can grey
    /// out the slots it knows the API would refuse instead of letting the driver
    /// pick one and read the refusal afterwards.
    /// </summary>
    [HttpGet("availability")]
    public async Task<BaseResponse<DriverAvailabilityOutput>> Availability([FromQuery] int? ignoreTripId)
        => await driverAvailabilityService.GetMySchedule(ignoreTripId);

    /// <summary>Where the caller's driver application stands, with its documents.</summary>
    [HttpGet("verification")]
    public async Task<BaseResponse<DriverVerificationOutput>> Verification()
        => await driverOnboardingService.GetStatus();

    /// <summary>Uploads (or replaces) one licence / id document.</summary>
    [HttpPost("documents")]
    [RequestSizeLimit(DriverDocumentRules.MaxBytes + 1024 * 1024)]
    public async Task<BaseResponse<DriverDocumentOutput>> UploadDocument([FromForm] UploadDriverDocumentInput input)
        => await driverOnboardingService.UploadDocument(input);

    [HttpDelete("documents/{id:int}")]
    public async Task<BaseResponse> DeleteDocument(int id)
        => await driverOnboardingService.DeleteDocument(id);

    /// <summary>
    /// Streams one of the caller's own documents back so the app can show a
    /// thumbnail of what it uploaded.
    ///
    /// The one endpoint in the API that answers outside the
    /// <see cref="BaseResponse"/> envelope — a JPEG cannot live in a JSON field,
    /// and base64 would cost a third of the bytes for nothing. Failures still
    /// return the usual envelope, so clients handle them exactly as elsewhere.
    /// </summary>
    [HttpGet("documents/{id:int}/content")]
    public async Task<IActionResult> DocumentContent(int id)
    {
        var response = await driverOnboardingService.OpenDocument(id);
        if (!response.Success || response.Data == null) return Ok(response);
        return File(response.Data.Content, response.Data.ContentType);
    }

    /// <summary>Submits license + documents for manual admin verification.</summary>
    [HttpPost("apply")]
    public async Task<BaseResponse> Apply([FromBody] DriverApplyInput input)
        => await driverOnboardingService.Apply(input);
}
