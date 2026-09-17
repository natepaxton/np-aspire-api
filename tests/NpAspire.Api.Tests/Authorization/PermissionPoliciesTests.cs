using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NpAspire.Api.Authorization;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.Authorization;

public class PermissionPoliciesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<string> AllPermissions => [.. Permissions.All];

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task EveryPermission_HasAPolicyNamedAfterIt(string permission)
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await provider.GetPolicyAsync(permission));
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Policy_Succeeds_WhenTheTokenGrantsThePermission(string permission)
    {
        var result = await AuthorizeAsync(permission, granted: permission);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Policy_Fails_WhenTheTokenGrantsNothing(string permission)
    {
        var result = await AuthorizeAsync(permission);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Policy_Fails_WhenTheTokenGrantsADifferentPermission()
    {
        var result = await AuthorizeAsync(Permissions.ReadUsers, granted: Permissions.ReadProfile);

        Assert.False(result.Succeeded);
    }

    private async Task<AuthorizationResult> AuthorizeAsync(string policy, params string[] granted)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "auth0|test-user"), .. granted.Select(p => new Claim(Permissions.ClaimName, p))],
            authenticationType: "test"));

        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        return await authorization.AuthorizeAsync(user, resource: null, policy);
    }
}
