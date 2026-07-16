using CrestApps.OrchardCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;
using SignalRRedisOptions = Microsoft.AspNetCore.SignalR.StackExchangeRedis.RedisOptions;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests;

public sealed class RedisBackplaneOptionsTests
{
    [Fact]
    public void ConfigureServices_WithoutValidOrchardRedisRegistration_DoesNotRegisterBackplane()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new RedisBackplaneStartup().ConfigureServices(services);

        // Assert
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IConfigureOptions<SignalRRedisOptions>));
    }

    [Fact]
    public void ConfigureServices_ChannelPrefixIncludesDeploymentPrefixAndShellName()
    {
        // Arrange
        var tenantAOptions = ResolveOptions("TenantA");
        var tenantBOptions = ResolveOptions("TenantB");

        // Act
        var tenantAPrefix = tenantAOptions.Configuration.ChannelPrefix.ToString();
        var tenantBPrefix = tenantBOptions.Configuration.ChannelPrefix.ToString();

        // Assert
        Assert.Equal("test-environment:TenantA:SignalR", tenantAPrefix);
        Assert.Equal("test-environment:TenantB:SignalR", tenantBPrefix);
        Assert.NotEqual(tenantAPrefix, tenantBPrefix);
    }

    private static SignalRRedisOptions ResolveOptions(string tenantName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ShellSettings
        {
            Name = tenantName,
        });
        services.AddSingleton<IRedisService>(_ =>
            throw new InvalidOperationException("The options test does not resolve Orchard's Redis service."));
        services.Configure<RedisOptions>(options =>
        {
            options.Configuration = "localhost:6379";
            options.ConfigurationOptions = ConfigurationOptions.Parse(options.Configuration);
            options.InstancePrefix = "test-environment:";
        });

        new RedisBackplaneStartup().ConfigureServices(services);

        return services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<SignalRRedisOptions>>()
            .Value;
    }
}
