using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class AIDataSourceDisplayDriverTests
{
    [Fact]
    public void BuildGroupedIndexProfileItems_UsesLocalizedProviderDisplayNames()
    {
        var method = GetBuildGroupedIndexProfileItemsMethod();
        var indexingOptions = new IndexingOptions();
        indexingOptions.AddIndexingProvider("azureai", provider => provider.DisplayName = new LocalizedString("Azure AI Search", "Azure AI Search"));
        indexingOptions.AddIndexingProvider("elasticsearch", provider => provider.DisplayName = new LocalizedString("Elasticsearch", "Elasticsearch"));

        var indexProfiles = new[]
        {
            new IndexProfile { Name = "content-azure", ProviderName = "azureai" },
            new IndexProfile { Name = "content-elastic", ProviderName = "elasticsearch" },
        };

        var items = (IEnumerable<SelectListItem>)method.Invoke(null, [indexProfiles, indexingOptions])!;
        var groupedItems = items.ToList();

        Assert.Collection(groupedItems,
            item =>
            {
                Assert.Equal("content-azure", item.Value);
                Assert.Equal("Azure AI Search", item.Group?.Name);
            },
            item =>
            {
                Assert.Equal("content-elastic", item.Value);
                Assert.Equal("Elasticsearch", item.Group?.Name);
            });
    }

    private static MethodInfo GetBuildGroupedIndexProfileItemsMethod()
    {
        var type = typeof(CrestApps.OrchardCore.AI.DataSources.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.Drivers.AIDataSourceDisplayDriver", throwOnError: true)!;

        return type.GetMethod("BuildGroupedIndexProfileItems", BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}
