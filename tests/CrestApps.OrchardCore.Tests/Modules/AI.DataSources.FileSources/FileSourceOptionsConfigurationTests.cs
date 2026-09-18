using CrestApps.Core.AI.FileSources;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.FileSources;

/// <summary>
/// Checks that configuration supplies the effort knobs and never the allowed roots.
/// </summary>
public sealed class FileSourceOptionsConfigurationTests
{
    [Fact]
    public void Configure_TakesTheEffortKnobsFromShellConfiguration()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:AI:FileSources:MaxItemsPerRun"] = "17",
            ["CrestApps:AI:FileSources:MaxConcurrentFetches"] = "5",
            ["CrestApps:AI:FileSources:DefaultRunIntervalMinutes"] = "90",
        });

        Assert.Equal(17, options.MaxItemsPerRun);
        Assert.Equal(5, options.MaxConcurrentFetches);
        Assert.Equal(90, options.DefaultRunIntervalMinutes);
    }

    [Fact]
    public void Configure_PinsTheAllowedRootToTheTenantFolder()
    {
        var options = Configure([]);

        Assert.Equal(["/tenants/TenantA/file-sources"], options.AllowedLocalRoots);
    }

    [Fact]
    public void Configure_DiscardsAnAllowedRootFromConfiguration()
    {
        // The framework's allow-list is host-wide, which is the wrong shape here: a tenant administrator
        // able to add an entry would be able to read anything the host process can open. One surviving
        // entry is the whole boundary gone, so the list is cleared rather than appended to.
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:AI:FileSources:AllowedLocalRoots:0"] = "/",
            ["CrestApps:AI:FileSources:AllowedLocalRoots:1"] = "/etc",
        });

        Assert.Equal(["/tenants/TenantA/file-sources"], options.AllowedLocalRoots);
    }

    [Fact]
    public void Configure_IgnoresTheSectionWrittenWithTheHostPrefix()
    {
        // Shell configuration is already rooted at the tenant's OrchardCore section, so the prefix is not
        // repeated in code. A value written with it here should not be picked up.
        var options = Configure(new Dictionary<string, string>
        {
            ["OrchardCore:CrestApps:AI:FileSources:MaxItemsPerRun"] = "17",
        });

        Assert.Equal(new FileSourceOptions().MaxItemsPerRun, options.MaxItemsPerRun);
    }

    private static FileSourceOptions Configure(Dictionary<string, string> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(x => x.GetSection(It.IsAny<string>()))
            .Returns<string>(configuration.GetSection);

        var tenantRoot = new Mock<ITenantFileSourceRoot>();
        tenantRoot.Setup(x => x.GetRoot()).Returns("/tenants/TenantA/file-sources");

        var options = new FileSourceOptions();

        new FileSourceOptionsConfiguration(shellConfiguration.Object, tenantRoot.Object).Configure(options);

        return options;
    }
}
