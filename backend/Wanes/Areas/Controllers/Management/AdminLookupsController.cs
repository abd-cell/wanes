using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

/// <summary>
/// Option lists for the CMS reference pickers, so a form can offer names and trip
/// details while the payload still carries a foreign key. Supported types:
/// users, drivers, riders, vehicles, trips, bookings, requests.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/lookups")]
public class AdminLookupsController : BaseApiController
{
    private readonly IAdminLookupService service;

    public AdminLookupsController(IAdminLookupService service) => this.service = service;

    [HttpGet("{type}")]
    public async Task<BaseResponse<PageOutput<LookupRow>>> Search(string type, [FromQuery] PageInput page)
        => await service.Search(type, page);

    [HttpGet("{type}/{id:int}")]
    public async Task<BaseResponse<LookupRow>> Get(string type, int id) => await service.Get(type, id);
}
