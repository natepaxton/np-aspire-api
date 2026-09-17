using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NpAspire.Api.Authorization;

namespace NpAspire.Api.Controllers;

/// <summary>
/// Authentication diagnostics. Login, logout, and token issuing are handled by Auth0, not by this API.
/// </summary>
[ApiController]
[Authorize]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Returns 200 when the caller has a valid access token granting <c>read:profile</c>. Without a usable token the
    /// response is 401; with a token that grants no such permission (a user with no role) it is 403.
    /// </summary>
    [HttpGet("check")]
    [Authorize(Policy = Permissions.ReadProfile)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult Check() => Ok();
}
