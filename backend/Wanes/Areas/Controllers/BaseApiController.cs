using Microsoft.AspNetCore.Mvc;

namespace Wanes.Areas.Controllers;

/// <summary>
/// Base for all API controllers. Actions stay thin: call one service method and
/// return its <c>BaseResponse</c>. Routes are <c>/api/v1/[controller]</c>.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase;
