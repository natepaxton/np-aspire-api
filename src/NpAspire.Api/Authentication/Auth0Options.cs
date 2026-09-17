using System.ComponentModel.DataAnnotations;

namespace NpAspire.Api.Authentication;

/// <summary>Auth0 settings (configuration section <c>Auth0</c>). Neither value is secret.</summary>
public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    /// <summary>Tenant domain, e.g. <c>dev-xxxx.us.auth0.com</c> (a leading <c>https://</c> is tolerated).</summary>
    [Required]
    public string Domain { get; init; } = string.Empty;

    /// <summary>Identifier of the Auth0 API; access tokens must be issued for this audience.</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>Token issuer and metadata base address: <c>https://{domain}/</c>.</summary>
    public string Authority
    {
        get
        {
            var host = Domain.Trim();
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = host["https://".Length..];
            }

            return $"https://{host.TrimEnd('/')}/";
        }
    }
}
