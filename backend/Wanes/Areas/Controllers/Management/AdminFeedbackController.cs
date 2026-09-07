using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

/// <summary>
/// The support desk's inbox for complaints and suggestions. Users file and read
/// their own through <see cref="Support.FeedbackController"/>.
///
/// No POST: a submission belongs to whoever wrote it, so there is nothing for an
/// admin to create here.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/feedback")]
public class AdminFeedbackController : BaseApiController
{
    private readonly IAdminFeedbackService service;

    public AdminFeedbackController(IAdminFeedbackService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<FeedbackRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] FeedbackKind? kind,
        [FromQuery] FeedbackStatus? status)
        => await service.List(page, kind, status);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<FeedbackRow>> Get(int id) => await service.Get(id);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<FeedbackRow>> Update(int id, [FromBody] FeedbackReviewInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
