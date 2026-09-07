using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Support;
using Wanes.Areas.Services.Support.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Support;

/// <summary>
/// Complaints and suggestions, from the user's side.
///
/// Authorized, unlike <see cref="FaqController"/> next to it: a submission the
/// desk cannot reply to is a dead end, and an anonymous endpoint here is an
/// open mailbox for anyone with the URL.
/// </summary>
[AppAuthorize]
[Route("api/v1/feedback")]
public class FeedbackController : BaseApiController
{
    private readonly IFeedbackService service;

    public FeedbackController(IFeedbackService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<List<FeedbackOutput>>> Mine() => await service.Mine();

    [HttpPost]
    public async Task<BaseResponse<FeedbackOutput>> Submit([FromBody] FeedbackInput input)
        => await service.Submit(input);
}
