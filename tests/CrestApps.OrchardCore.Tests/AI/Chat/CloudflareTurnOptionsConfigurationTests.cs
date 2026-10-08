using CrestApps.Core.AI.Chat.Realtime;
using CrestApps.OrchardCore.AI.Chat.Core;
using CrestApps.OrchardCore.Tests.Framework.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.AI.Chat;

public sealed class CloudflareTurnOptionsConfigurationTests
{
    [Fact]
    public void AddTenantCloudflareRealtimeTurn_BindsTheTokenFromTheTenantConfiguration()
    {
        var options = GetOptions(new Dictionary<string, string>
        {
            ["CrestApps:AI:RealtimeTransport:Cloudflare:TokenId"] = " tenant-token-id ",
            ["CrestApps:AI:RealtimeTransport:Cloudflare:ApiToken"] = "tenant-api-token",
            ["CrestApps:AI:RealtimeTransport:Cloudflare:TtlSeconds"] = "3600",
        });

        Assert.Equal("tenant-token-id", options.TokenId);
        Assert.Equal("tenant-api-token", options.ApiToken);
        Assert.Equal(3600, options.TtlSeconds);
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void AddTenantCloudflareRealtimeTurn_LeavesTheOptionsUnconfigured_WhenTheTenantDeclaresNothing()
    {
        var options = GetOptions([]);

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void AddTenantCloudflareRealtimeTurn_IgnoresASectionUnderAnotherName()
    {
        var options = GetOptions(new Dictionary<string, string>
        {
            ["CrestApps:AI:RealtimeTransport:TurnUsername"] = "not-cloudflare",
        });

        Assert.Null(options.TokenId);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void AddTenantCloudflareRealtimeTurn_LetsTheTenantOverrideTheHost()
    {
        var options = GetOptions(
            new Dictionary<string, string>
            {
                ["CrestApps:AI:RealtimeTransport:Cloudflare:TokenId"] = "tenant-token-id",
            },
            new Dictionary<string, string>
            {
                ["CrestApps:AI:RealtimeTransport:Cloudflare:TokenId"] = "host-token-id",
                ["CrestApps:AI:RealtimeTransport:Cloudflare:ApiToken"] = "host-api-token",
            });

        Assert.Equal("tenant-token-id", options.TokenId);

        // A value the tenant leaves alone keeps whatever the host provided.
        Assert.Equal("host-api-token", options.ApiToken);
    }

    private static CloudflareTurnOptions GetOptions(
        Dictionary<string, string> tenantValues,
        Dictionary<string, string> hostValues = null)
    {
        var tenantConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(tenantValues)
            .Build();

        // Orchard Core resolves IConfiguration to the host's configuration inside a tenant container, which
        // is what CrestApps.Core binds. The tenant's own configuration only reaches it through IShellConfiguration.
        var hostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(hostValues ?? [])
            .Build();

        var services = new ServiceCollection();

        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(hostConfiguration);
        services.AddSingleton<IShellConfiguration>(new TestShellConfiguration(tenantConfiguration));
        services.AddTenantCloudflareRealtimeTurn();

        using var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IOptions<CloudflareTurnOptions>>().Value;
    }
}
