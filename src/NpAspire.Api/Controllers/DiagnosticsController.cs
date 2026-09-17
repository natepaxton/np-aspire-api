using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NpAspire.Api.Common;

namespace NpAspire.Api.Controllers;

/// <summary>Public diagnostics for checking that the API and its dependencies are up.</summary>
[ApiController]
[Route("diagnostics")]
public sealed class DiagnosticsController(HealthCheckService healthChecks, IHostEnvironment environment)
    : ControllerBase
{
    /// <summary>
    /// Runs every registered health check. <c>Data</c> is the overall <see cref="HealthStatus"/>
    /// (0 Unhealthy, 1 Degraded, 2 Healthy); an unhealthy API returns 503. Anonymous, so only the overall status is
    /// returned, never check names or details.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ServerResult<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ServerResult<int>>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ServerResult<int>>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServerResult<int>>> CheckStatus(CancellationToken cancellationToken)
    {
        var result = new ServerResult<int>();

        try
        {
            var report = await healthChecks.CheckHealthAsync(cancellationToken);
            result.Data = (int)report.Status;

            switch (report.Status)
            {
                case HealthStatus.Unhealthy:
                    result.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    result.ErrorMessages.Add("One or more health checks failed.");
                    break;
                case HealthStatus.Degraded:
                    result.WarningMessages.Add("One or more health checks reported degraded.");
                    break;
            }

            return StatusCode(result.StatusCode, result);
        }
        catch (Exception exception)
        {
            result.StatusCode = StatusCodes.Status500InternalServerError;
            result.AddError(exception, includeStackTrace: environment.IsDevelopment());
            return StatusCode(result.StatusCode, result);
        }
    }
}
