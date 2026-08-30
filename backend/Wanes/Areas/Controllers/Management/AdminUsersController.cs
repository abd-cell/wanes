using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/users")]
public class AdminUsersController : BaseApiController
{
    private readonly IAdminUserService service;

    public AdminUsersController(IAdminUserService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<UserRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] DriverStatus? driverStatus,
        [FromQuery] bool? isDriver,
        [FromQuery] bool? isDisabled)
        => await service.List(page, driverStatus, isDriver, isDisabled);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<UserRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<UserRow>> Create([FromBody] UserInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<UserRow>> Update(int id, [FromBody] UserInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);

    [HttpPost("{id:int}/roles")]
    public async Task<BaseResponse<UserRow>> SetRole(int id, [FromBody] RoleInput input)
        => await service.SetRole(id, input);
}
