using Microsoft.AspNetCore.Mvc;

namespace Akay.Be.Host.Controllers;

[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController : ControllerBase
{
    [HttpGet]
    //[Authorize(Roles = "writer")]
    public IActionResult Get()
    {
        throw new NotImplementedException();
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public IActionResult Create([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] object request)
    {
        throw new NotImplementedException();
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        throw new NotImplementedException();
    }
}
