using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
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
}
