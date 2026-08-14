using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Scripting;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

public sealed class RecipesStartupTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterSingletonSafeGlobalMethodProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ITemplateService>());

        new CrestApps.OrchardCore.AI.RecipesStartup().ConfigureServices(services);

        services.AddSingleton<GlobalMethodProviderConsumer>();

        var providerDescriptor = Assert.Single(services, service => service.ServiceType == typeof(IGlobalMethodProvider));
        Assert.Equal(ServiceLifetime.Singleton, providerDescriptor.Lifetime);

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });
        using var scope = serviceProvider.CreateScope();

        var consumer = scope.ServiceProvider.GetRequiredService<GlobalMethodProviderConsumer>();

        Assert.NotEmpty(consumer.MethodProviders);
    }

    private sealed class GlobalMethodProviderConsumer(IEnumerable<IGlobalMethodProvider> methodProviders)
    {
        public IReadOnlyList<IGlobalMethodProvider> MethodProviders { get; } = methodProviders.ToArray();
    }
}
