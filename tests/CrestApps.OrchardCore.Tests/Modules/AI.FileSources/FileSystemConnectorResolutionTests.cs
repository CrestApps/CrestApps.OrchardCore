using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.OrchardCore.AI.FileSources;
using CrestApps.OrchardCore.AI.FileSources.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileSources;

/// <summary>
/// Checks that the folders the <c>FileSystem</c> connector may read are this tenant's, and only this
/// tenant's.
/// </summary>
/// <remarks>
/// <para>
/// The framework's connector is bounded by <c>FileSystemConnectorOptions</c>, which is a host-wide
/// allow-list. On a multi-tenant host that is the wrong shape: whatever the host configured would be
/// readable by every tenant. The feature takes that list back and puts the tenant's own folder in its
/// place.
/// </para>
/// <para>
/// Nothing about that is visible at the call site. It works only because it post-configures, running after
/// the framework's own configuration step rather than before it. Register it as an
/// <c>IConfigureOptions</c> instead, or drop it, and file sources keep working while reading straight from
/// the host. That is the failure these tests exist to make loud.
/// </para>
/// </remarks>
public sealed class FileSystemConnectorResolutionTests
{
    private const string TenantRoot = "/tenants/TenantA/file-sources";

    [Fact]
    public void TheTenantFolder_IsTheOnlyAllowedRoot()
    {
        using var provider = BuildFeature();

        var options = provider.GetRequiredService<IOptions<FileSystemConnectorOptions>>().Value;

        Assert.Equal([TenantRoot], options.AllowedRoots);
    }

    [Fact]
    public void TheTenantFolder_IsWhatRelativePathsResolveAgainst()
    {
        // Without this a file source's stored folder would resolve against the process's current
        // directory, which is not the tenant's folder and is not even the application's on every host.
        using var provider = BuildFeature();

        var options = provider.GetRequiredService<IOptions<FileSystemConnectorOptions>>().Value;

        Assert.Equal(TenantRoot, options.BasePath);
    }

    [Fact]
    public void AHostConfiguredRoot_DoesNotSurvive()
    {
        // The framework carries FileSourceOptions.AllowedLocalRoots into the connector's own allowed roots.
        // A single surviving entry is the whole tenant boundary gone, so this is the ordering guard: it
        // fails if the feature's step ever runs before the framework's rather than after it.
        using var provider = BuildFeature(hostRoot: "/etc");

        var options = provider.GetRequiredService<IOptions<FileSystemConnectorOptions>>().Value;

        Assert.Equal([TenantRoot], options.AllowedRoots);
    }

    [Fact]
    public void TheConnector_IsStillOnOffer()
    {
        // Confining the connector replaces the folders it may read, not the descriptor the create screen
        // lists. If it took the descriptor with it, no one could add a file-system file source at all.
        using var provider = BuildFeature();

        var descriptors = provider.GetRequiredService<IOptions<IngestionConnectorOptions>>().Value.Connectors;

        Assert.Contains(descriptors, descriptor => descriptor.Name == FileSystemIngestionConnector.ConnectorName);
    }

    private static ServiceProvider BuildFeature(string hostRoot = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // The feature's own registrations, so a change to Startup is what these tests see.
        new Startup().ConfigureServices(services);

        // Ambient services Orchard Core supplies to a module, stubbed. Registered after the feature so the
        // stub wins: the tenant folder is computed from shell settings that a bare container has not got.
        var settings = new Dictionary<string, string>();

        if (hostRoot is not null)
        {
            settings[$"{FileSourceOptionsConfiguration.ConfigurationSectionName}:AllowedLocalRoots:0"] = hostRoot;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(x => x.GetSection(It.IsAny<string>()))
            .Returns<string>(configuration.GetSection);

        var tenantRoot = new Mock<ITenantFileSourceRoot>();
        tenantRoot.Setup(x => x.GetRoot()).Returns(TenantRoot);

        // The framework's own configuration step takes the content root from here. Orchard supplies it to
        // a real tenant; a bare container has to be told.
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns("/app");

        services.AddSingleton(environment.Object);
        services.AddSingleton(shellConfiguration.Object);
        services.AddSingleton(tenantRoot.Object);

        return services.BuildServiceProvider();
    }
}
