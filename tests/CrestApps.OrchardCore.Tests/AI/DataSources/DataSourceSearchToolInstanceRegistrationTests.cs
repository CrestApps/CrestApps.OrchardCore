using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.AI.DataSources;

public sealed class DataSourceSearchToolInstanceRegistrationTests
{
    [Fact]
    public void DataSourcesToolInstancesStartup_RegistersTheDataSourceSearchSource()
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddLogging();

        new DataSourcesToolInstancesStartup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIOptions>>().Value;

        Assert.True(options.ToolInstanceSources.ContainsKey(DataSourceSearchToolConstants.SourceName));

        var source = serviceProvider.GetKeyedService<IAIToolInstanceSource>(DataSourceSearchToolConstants.SourceName);

        Assert.IsType<DataSourceSearchToolInstanceSource>(source);
    }

    /// <summary>
    /// The source is registered by a second feature, after the tool instances feature has already registered
    /// its own. Both sets must survive: a source list that silently loses one of them is exactly what the
    /// "Available Sources" dialog renders.
    /// </summary>
    [Fact]
    public void DataSourcesToolInstancesStartup_AddsToTheSourcesTheToolInstancesFeatureRegistered()
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddLogging();

        new ToolInstancesStartup().ConfigureServices(services);
        new DataSourcesToolInstancesStartup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var sources = serviceProvider.GetRequiredService<IOptions<AIOptions>>().Value.ToolInstanceSources;

        Assert.Contains(DataSourceSearchToolConstants.SourceName, sources.Keys);
        Assert.Contains(HttpApiRequestToolConstants.SourceName, sources.Keys);
        Assert.Contains(DocumentationToolConstants.WebsiteSearchSourceName, sources.Keys);
    }
}
