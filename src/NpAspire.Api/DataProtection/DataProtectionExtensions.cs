using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace NpAspire.Api.DataProtection;

public static class DataProtectionExtensions
{
    /// <summary>
    /// Keeps Data Protection keys in memory. The API protects nothing today (bearer tokens only: no cookies, sessions,
    /// or antiforgery), but ASP.NET Core still creates a key at startup. By default that key is written unencrypted to
    /// the container's disk, which logs two warnings on every start.
    /// </summary>
    /// <remarks>
    /// Keys are regenerated on every start and differ between instances. Before anything relies on protected data
    /// (for example BFF cookie auth, docs/spec.md §8), persist the keys to shared storage and encrypt them instead.
    /// </remarks>
    public static IServiceCollection AddInMemoryDataProtectionKeys(this IServiceCollection services)
    {
        services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = new InMemoryXmlRepository();
            // The keys never leave the process, so there is nothing to encrypt them against.
            options.XmlEncryptor = new NullXmlEncryptor();
        });

        return services;
    }
}
