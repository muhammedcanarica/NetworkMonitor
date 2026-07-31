using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NetworkMonitor.Api.Controllers;

[ApiController, Route("api/security")]
public sealed class SecurityController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public ActionResult GetCsrfToken() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
}
