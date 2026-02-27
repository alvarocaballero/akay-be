using Microsoft.AspNetCore.Mvc;

namespace Akay.Be.Host.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Akay.Be service is healthy.");
}
