using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class AIDataSourceDisplayDriverTests
{
    [Fact]
    public void BuildGroupedIndexProfileItems_UsesLocalizedProviderDisplayNames()
    {
        var indexingOptions = new IndexingOptions();
        indexingOptions.AddIndexingProvider("azureai", provider => provider.DisplayName = new LocalizedString("Azure AI Search", "Azure AI Search"));
        indexingOptions.AddIndexingProvider("elasticsearch", provider => provider.DisplayName = new LocalizedString("Elasticsearch", "Elasticsearch"));
        var type = GetAIDataSourceDisplayDriverType();
        var driver = Activator.CreateInstance(
            type,
            Mock.Of<IIndexProfileStore>(),
            Options.Create(indexingOptions),
            null)!;
        var method = GetBuildGroupedIndexProfileItemsMethod(type);

        var indexProfiles = new[]
        {
            new IndexProfile { Name = "content-azure", ProviderName = "azureai" },
            new IndexProfile { Name = "content-elastic", ProviderName = "elasticsearch" },
        };

        var items = (IEnumerable<SelectListItem>)method.Invoke(driver, [indexProfiles])!;
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

    private static Type GetAIDataSourceDisplayDriverType()
    {
        return typeof(CrestApps.OrchardCore.AI.DataSources.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.Drivers.AIDataSourceDisplayDriver", throwOnError: true)!;
    }

    private static MethodInfo GetBuildGroupedIndexProfileItemsMethod(Type type)
    {
        return type.GetMethod("BuildGroupedIndexProfileItems", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }
}
