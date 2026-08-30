using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/sessions")]
public class AdminSessionsController : BaseApiController
{
    private readonly IAdminUserLoginService service;

    public AdminSessionsController(IAdminUserLoginService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<UserLoginRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] DeviceType? deviceType,
        [FromQuery] int? userId)
        => await service.List(page, deviceType, userId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<UserLoginRow>> Get(int id) => await service.Get(id);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
