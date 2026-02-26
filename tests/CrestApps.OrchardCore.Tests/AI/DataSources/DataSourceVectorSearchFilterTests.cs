using System.Reflection;
using System.Text;

namespace CrestApps.OrchardCore.Tests.AI.DataSources;

public sealed class DataSourceVectorSearchFilterTests
{
    [Fact]
    public void AzureAISearch_BuildODataFilter_AlwaysIncludesDataSourceId()
    {
        var serviceType = Type.GetType(
            "CrestApps.OrchardCore.AI.DataSources.AzureAI.Services.AzureAISearchDataSourceContentManager, CrestApps.OrchardCore.AI.DataSources.AzureAI",
            throwOnError: true);

        var method = serviceType!.GetMethod("BuildODataFilter", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, ["ds1", null])!;

        Assert.Equal("dataSourceId eq 'ds1'", result);
    }

    [Fact]
    public void AzureAISearch_BuildODataFilter_MergesUserFilterWithDataSourceId()
    {
        var serviceType = Type.GetType(
            "CrestApps.OrchardCore.AI.DataSources.AzureAI.Services.AzureAISearchDataSourceContentManager, CrestApps.OrchardCore.AI.DataSources.AzureAI",
            throwOnError: true);

        var method = serviceType!.GetMethod("BuildODataFilter", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, ["ds1", "(myField eq 'x')"])!;

        Assert.Equal("(dataSourceId eq 'ds1') and ((myField eq 'x'))", result);
    }

    [Fact]
    public void Elasticsearch_BuildMustQueryDebug_AlwaysIncludesDataSourceId()
    {
        var serviceType = Type.GetType(
            "CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services.ElasticsearchDataSourceContentManager, CrestApps.OrchardCore.AI.DataSources.Elasticsearch",
            throwOnError: true);

        var method = serviceType.GetMethod("BuildMustQueryDebug", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var list = (System.Collections.IEnumerable)method.Invoke(null, ["ds1", null])!;

        var items = list.Cast<object>().ToList();
        Assert.Single(items);

        Assert.Equal("term", items[0].GetType().GetField("Item1").GetValue(items[0]));
        Assert.Equal("ds1", items[0].GetType().GetField("Item2").GetValue(items[0]));
    }

    [Fact]
    public void Elasticsearch_BuildMustQueryDebug_AddsWrapperQueryWithBase64Filter()
    {
        var serviceType = Type.GetType(
            "CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services.ElasticsearchDataSourceContentManager, CrestApps.OrchardCore.AI.DataSources.Elasticsearch",
            throwOnError: true);

        var method = serviceType.GetMethod("BuildMustQueryDebug", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        const string filter = "{\"term\":{\"filters.category\":\"books\"}}";

        var list = (System.Collections.IEnumerable)method.Invoke(null, ["ds1", filter])!;
        var items = list.Cast<object>().ToList();

        Assert.Equal(2, items.Count);

        Assert.Equal("term", items[0].GetType().GetField("Item1").GetValue(items[0]));
        Assert.Equal("ds1", items[0].GetType().GetField("Item2").GetValue(items[0]));

        Assert.Equal("wrapper", items[1].GetType().GetField("Item1").GetValue(items[1]));

        var base64 = (string)items[1].GetType().GetField("Item2").GetValue(items[1])!;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        Assert.Equal(filter, decoded);
    }
}
