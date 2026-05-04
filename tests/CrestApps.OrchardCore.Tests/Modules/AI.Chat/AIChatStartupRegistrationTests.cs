using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Handlers;
using CrestApps.OrchardCore.AI.Chat.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIChatStartupRegistrationTests
{
    [Fact]
    public void ConfigureServices_RegistersAdminMenuProfileCacheServices()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.Chat.Startup().ConfigureServices(services);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAIProfileAdminMenuCacheService) &&
            descriptor.ImplementationType == typeof(DefaultAIProfileAdminMenuCacheService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICatalogEntryHandler<AIProfile>) &&
            descriptor.ImplementationType == typeof(AIProfileAdminMenuCacheHandler) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
