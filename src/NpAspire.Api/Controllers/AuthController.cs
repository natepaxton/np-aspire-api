using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NpAspire.Api.Controllers;

/// <summary>
/// Authentication diagnostics. Login, logout, and token issuing are handled by Auth0, not by this API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Returns 200 when the request has a valid access token for this API, otherwise 401.</summary>
    [HttpGet("check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Check() => Ok();
}
