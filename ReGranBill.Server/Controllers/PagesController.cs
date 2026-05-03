using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/pages")]
[Authorize]
public class PagesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        var pages = PageCatalog.All.Select(p => new
        {
            key = p.Key,
            label = p.Label,
            group = p.Group,
            groupLabel = p.GroupLabel,
            hidden = p.Hidden
        });

        return Ok(pages);
    }
}
