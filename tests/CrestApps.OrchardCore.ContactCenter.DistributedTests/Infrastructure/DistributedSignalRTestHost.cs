using CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;
using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;

internal sealed class DistributedSignalRTestHost : IAsyncDisposable
{
    private WebApplication _app;

    public DistributedSignalRTestHost(
        string nodeName,
        string tenantName,
        string redisConfiguration)
    {
        NodeName = nodeName;
        TenantName = tenantName;
        RedisConfiguration = redisConfiguration;
    }

    public string BaseUrl { get; private set; }

    public string NodeName { get; }

    public string TenantName { get; }

    private string RedisConfiguration { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new ShellSettings
        {
            Name = TenantName,
        });
        builder.Services.Configure<global::OrchardCore.Redis.RedisOptions>(options =>
        {
            options.Configuration = RedisConfiguration;
            options.ConfigurationOptions = ConfigurationOptions.Parse(RedisConfiguration);
            options.InstancePrefix = "contact-center-distributed-tests:";
        });
        builder.Services.AddSingleton<IRedisService>(_ =>
            throw new InvalidOperationException("The transport harness does not resolve Orchard's Redis service."));
        builder.Services.AddSingleton<TestProviderListener>();

        new RedisBackplaneStartup().ConfigureServices(builder.Services);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapHub<DistributedTestHub>("/distributed");

        await app.StartAsync(cancellationToken);

        var addresses = app.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            .Addresses;

        BaseUrl = addresses.First();
        _app = app;
    }

    public TestProviderListener GetProviderListener()
    {
        return _app.Services.GetRequiredService<TestProviderListener>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
