using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NpAspire.Api.DataProtection;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.DataProtection;

public class DataProtectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Keys_AreKeptInMemory()
    {
        var options = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.IsType<InMemoryXmlRepository>(options.XmlRepository);
        Assert.IsType<NullXmlEncryptor>(options.XmlEncryptor);
    }

    [Fact]
    public void Protector_RoundTripsData()
    {
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

        Assert.Equal("payload", protector.Unprotect(protector.Protect("payload")));
    }
}
