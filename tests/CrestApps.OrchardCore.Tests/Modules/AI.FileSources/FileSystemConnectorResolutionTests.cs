using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Indexing;
using CrestApps.OrchardCore.AI.FileSources;
using CrestApps.OrchardCore.AI.FileSources.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileSources;

/// <summary>
/// Checks that the <c>FileSystem</c> connector a run resolves is the tenant-confined one.
/// </summary>
/// <remarks>
/// <para>
/// The framework's own connector is reachable by name, and on its own it is bounded only by
/// <c>FileSourceOptions.AllowedLocalRoots</c> -- a host-wide allow-list compared as text, which does not
/// follow a link. The wrapper is what resolves the configured folder against this tenant's boundary before
/// anything is opened, so the whole boundary rests on the wrapper being the one that answers to the name.
/// </para>
/// <para>
/// Nothing about that is visible at the call site: both registrations use the same key and the same
/// interface, and the framework's is a <c>TryAdd</c>, so the override wins only because it runs after. Drop
/// it, reorder it, or move it ahead of <c>AddCoreFileSystemConnector</c>, and file sources keep working
/// while reading straight from the host. That is the failure this test exists to make loud.
/// </para>
/// </remarks>
public sealed class FileSystemConnectorResolutionTests
{
    [Fact]
    public void TheFileSystemConnector_ResolvesToTheTenantConfinedOne()
    {
        using var provider = BuildFeature();
        using var scope = provider.CreateScope();

        var connector = scope.ServiceProvider
            .GetRequiredKeyedService<IIngestionConnector>(FileSystemIngestionConnector.ConnectorName);

        Assert.IsType<TenantFileSystemIngestionConnector>(connector);
    }

    [Fact]
    public void TheOverride_LeavesTheConnectorOnOffer()
    {
        // The override replaces what the name resolves to, not the descriptor the create screen lists. If
        // it took the descriptor with it, no one could add a file-system file source at all.
        using var provider = BuildFeature();

        var descriptors = provider.GetRequiredService<IOptions<IngestionConnectorOptions>>().Value.Connectors;

        Assert.Contains(descriptors, descriptor => descriptor.Name == FileSystemIngestionConnector.ConnectorName);
    }

    private static ServiceProvider BuildFeature()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // The feature's own registrations, so a change to Startup is what this test sees.
        new Startup().ConfigureServices(services);

        // Ambient services Orchard Core supplies to a module, stubbed. Registered after the feature so the
        // stub wins: the tenant folder is computed from shell settings that a bare container has not got.
        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(x => x.GetSection(It.IsAny<string>()))
            .Returns<string>(new ConfigurationBuilder().Build().GetSection);

        var tenantRoot = new Mock<ITenantFileSourceRoot>();
        tenantRoot.Setup(x => x.GetRoot()).Returns("/tenants/TenantA/file-sources");

        services.AddSingleton(shellConfiguration.Object);
        services.AddSingleton(tenantRoot.Object);

        return services.BuildServiceProvider();
    }
}
