using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// Enabling the AI Usage Analytics feature is what turns usage tracking on, for every client kind the AI client
/// factory creates: chat, embeddings, images, speech-to-text, text-to-speech and realtime.
/// </summary>
public sealed class UsageAnalyticsStartupTests
{
    [Fact]
    public void ConfigureServices_RegistersTheUsageServiceAndObserver()
    {
        var services = new ServiceCollection();

        new UsageAnalyticsStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(IAICompletionUsageService)));
        Assert.True(isService.IsService(typeof(IAICompletionUsageObserver)));
    }

    [Fact]
    public void ConfigureServices_RegistersTheDefaultUsageService()
    {
        var services = new ServiceCollection();

        new UsageAnalyticsStartup().ConfigureServices(services);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAICompletionUsageService) &&
            descriptor.ImplementationType == typeof(DefaultAICompletionUsageService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
