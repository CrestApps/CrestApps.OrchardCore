using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Sends real requests from a parent tenant through the guarded <see cref="IHttpClientFactory"/> on a host that lets
/// tenants reach private networks, so the guarded connection itself opens: allowed traffic must still get through,
/// and the address of a tenant must still be refused.
/// </summary>
public sealed class EgressConnectionTests
{
    [Fact]
    public async Task AllowedRequest_FromAParent_OpensTheGuardedConnection_AndATenantAddressIsStillRefused()
    {
        // Arrange
        var password = $"Th-{Guid.NewGuid():N}!aA1";
        await using var host = await TenantHierarchyTestHost.StartAsync(new Dictionary<string, string>
        {
            ["TenantHierarchy:Egress:BlockPrivateNetworks"] = "false",
        });

        await host.SetupTenantAsync(ShellSettings.DefaultShellName, "Blank", "platform", password);
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var platform = (await features.GetAvailableFeaturesAsync()).Single(feature => feature.Id == TenantHierarchyConstants.Features.Platform);
            await features.EnableFeaturesAsync([platform], force: true);
        });

        await host.CreateTenantAsync("egressfirm", "egressfirm.localhost");
        await host.SetupTenantAsync("egressfirm", "Blank", "owner", password);
        await host.CreateTenantAsync("neighbor", "neighbor.localhost");
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var (made, _) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync("egressfirm", "egressfirm", "Egress Firm", new ParentTenantPolicy());
            Assert.True(made.Succeeded, made.Error);
        });

        // Act
        var (allowed, refused) = await host.InTenantAsync("egressfirm", async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            using var response = await client.GetAsync($"http://127.0.0.1:{host.Port}/", TestContext.Current.CancellationToken);
            var exception = await Record.ExceptionAsync(() => client.GetAsync($"http://neighbor.localhost:{host.Port}/", TestContext.Current.CancellationToken));

            return (response.StatusCode, exception);
        });

        // Assert: the platform answered, so the connection opened through the guard; the tenant address did not.
        Assert.True((int)allowed > 0);
        var refusal = Assert.IsType<HttpRequestException>(refused);
        Assert.Contains("tenant of this application", refusal.Message, StringComparison.Ordinal);
    }
}
