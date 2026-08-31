using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

/// <summary>
/// Admin CRUD over the help-centre FAQ. The published entries are served
/// anonymously by <see cref="Support.FaqController"/> for the clients to read.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/faqs")]
public class AdminFaqsController : BaseApiController
{
    private readonly IAdminFaqService service;

    public AdminFaqsController(IAdminFaqService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<FaqRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] FaqCategory? category,
        [FromQuery] bool? isPublished)
        => await service.List(page, category, isPublished);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<FaqRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<FaqRow>> Create([FromBody] FaqInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<FaqRow>> Update(int id, [FromBody] FaqInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
