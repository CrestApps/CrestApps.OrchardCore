using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Http;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class ElasticsearchDataConnectorTests
{
    private const string Mapping = """
        {
          "orders-2026": {
            "mappings": {
              "properties": {
                "total": { "type": "scaled_float", "scaling_factor": 100 },
                "count": { "type": "long" },
                "placed": { "type": "date" },
                "paid": { "type": "boolean" },
                "status": { "type": "keyword" },
                "customer": { "properties": { "name": { "type": "text", "fields": { "keyword": { "type": "keyword" } } } } },
                "lines": { "type": "nested" }
              }
            }
          }
        }
        """;

    [Fact]
    public void ParseMapping_MapsTypesAndFlattensObjectsByPath()
    {
        // Act
        using var document = JsonDocument.Parse(Mapping);
        var fields = ElasticsearchDataConnector.ParseMapping(document.RootElement);

        // Assert
        Assert.Equal(DataFieldType.Decimal, fields["total"].Type);
        Assert.Equal(DataFieldType.Integer, fields["count"].Type);
        Assert.Equal(DataFieldType.DateTime, fields["placed"].Type);
        Assert.Equal(DataFieldType.Boolean, fields["paid"].Type);
        Assert.Equal(DataFieldType.Text, fields["status"].Type);
        Assert.Equal(DataFieldType.Text, fields["customer.name"].Type);
        Assert.Equal(DataFieldType.Text, fields["lines"].Type);
    }

    [Fact]
    public void ParseMapping_ReadsALegacyTypedMapping()
    {
        // Act
        using var document = JsonDocument.Parse("""{ "old": { "mappings": { "_doc": { "properties": { "n": { "type": "integer" } } } } } }""");
        var fields = ElasticsearchDataConnector.ParseMapping(document.RootElement);

        // Assert
        Assert.Equal(DataFieldType.Integer, fields["n"].Type);
    }

    [Fact]
    public void BuildSearch_AppliesOnlyRangesOnNumbersAndDates_AndTheWatermark()
    {
        // Arrange
        using var document = JsonDocument.Parse(Mapping);
        var mapping = ElasticsearchDataConnector.ParseMapping(document.RootElement);
        var context = Context();
        context.Conditions.Add(new DataCondition { Field = "count", Operator = DataFilterOperator.GreaterThan, Values = [5L] });
        context.Conditions.Add(new DataCondition { Field = "status", Operator = DataFilterOperator.Equals, Values = ["open"] });
        context.Conditions.Add(new DataCondition { Field = "total", Operator = DataFilterOperator.Equals, Values = [9m] });
        context.WatermarkField = "placed";
        context.Watermark = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var search = JsonNode.Parse(ElasticsearchDataConnector.BuildSearch(context, new ElasticsearchDataSetSettings { Query = """{ "term": { "status": "open" } }""" }, mapping, ["_id", "count", "placed"], 500));

        // Assert
        var filters = search["query"]["bool"]["filter"].AsArray();
        Assert.Equal(2, filters.Count);
        Assert.Equal(5, filters[0]["range"]["count"]["gt"].GetValue<long>());
        Assert.Equal("2026-10-01T00:00:00.000Z", filters[1]["range"]["placed"]["gt"].GetValue<string>());
        Assert.Equal("open", search["query"]["bool"]["must"][0]["term"]["status"].GetValue<string>());
        Assert.Equal(["count", "placed"], search["_source"].AsArray().Select(node => node.GetValue<string>()));
        Assert.Equal(500, search["size"].GetValue<int>());
    }

    [Fact]
    public void BuildSearch_WithAWatermarkOnATextField_Fails()
    {
        // Arrange
        using var document = JsonDocument.Parse(Mapping);
        var mapping = ElasticsearchDataConnector.ParseMapping(document.RootElement);
        var context = Context();
        context.WatermarkField = "status";
        context.Watermark = "x";

        // Act & Assert
        Assert.Throws<DataConnectorException>(() => ElasticsearchDataConnector.BuildSearch(context, new ElasticsearchDataSetSettings(), mapping, ["status"], 10));
    }

    [Fact]
    public void BuildSearch_WithAQueryThatIsNotAnObject_Fails()
    {
        // Act & Assert
        Assert.Throws<DataConnectorException>(() =>
            ElasticsearchDataConnector.BuildSearch(Context(), new ElasticsearchDataSetSettings { Query = "[1]" }, new Dictionary<string, ElasticsearchField>(), ["_id"], 10));
    }

    [Fact]
    public async Task ReadAsync_ScrollsThroughEveryPage_ThenClearsTheScroll()
    {
        // Arrange
        var scrolls = 0;
        var client = DataConnectorHttpTests.CreateClient(request =>
        {
            var path = request.RequestUri.AbsolutePath;

            if (path.EndsWith("/_mapping", StringComparison.Ordinal))
            {
                return StubHttpHandler.Json(Mapping);
            }

            if (request.Method == HttpMethod.Delete)
            {
                return StubHttpHandler.Json("""{ "succeeded": true }""");
            }

            if (path.EndsWith("/_search", StringComparison.Ordinal))
            {
                return StubHttpHandler.Json("""{ "_scroll_id": "s1", "hits": { "hits": [ { "_id": "a", "_source": { "count": 1, "customer": { "name": "Ada" } } } ] } }""");
            }

            scrolls++;

            return StubHttpHandler.Json(scrolls == 1
                ? """{ "_scroll_id": "s2", "hits": { "hits": [ { "_id": "b", "_source": { "count": 2 } } ] } }"""
                : """{ "_scroll_id": "s2", "hits": { "hits": [] } }""");
        }, out var handler);

        var connector = new ElasticsearchDataConnector(client);
        var context = Context();
        context.Fields.Add("_id");
        context.Fields.Add("count");
        context.Fields.Add("customer.name");

        // Act
        var rows = new List<object[]>();

        await foreach (var batch in connector.ReadAsync(context, TestContext.Current.CancellationToken))
        {
            rows.AddRange(batch.Rows);
        }

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", 1L, "Ada"], rows[0]);
        Assert.Equal("b", rows[1][0]);
        Assert.Null(rows[1][2]);
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Delete && request.Body.Contains("s2", StringComparison.Ordinal));
        Assert.Contains("scroll=2m", handler.Requests.First(request => request.Uri.AbsolutePath.EndsWith("/_search", StringComparison.Ordinal)).Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_ListsIndicesAndAliases_ButNotSystemIndices()
    {
        // Arrange
        var client = DataConnectorHttpTests.CreateClient(request => request.RequestUri.AbsolutePath.Contains("_cat/indices", StringComparison.Ordinal)
            ? StubHttpHandler.Json("""[ { "index": "orders", "docs.count": "12" }, { "index": ".kibana" } ]""")
            : StubHttpHandler.Json("""[ { "alias": "current-orders" } ]"""), out _);

        // Act
        var resources = await new ElasticsearchDataConnector(client).DiscoverAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["orders", "current-orders"], resources.Select(resource => resource.Name));
        Assert.Equal("orders (12 documents)", resources[0].DisplayName);
    }

    [Fact]
    public async Task TestAsync_ReportsTheClusterVersion()
    {
        // Arrange
        var client = DataConnectorHttpTests.CreateClient(_ => StubHttpHandler.Json("""{ "cluster_name": "prod", "version": { "number": "8.15.0" } }"""), out _);

        // Act
        var result = await new ElasticsearchDataConnector(client).TestAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains("8.15.0", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestAsync_WhenTheClusterRefusesTheCredentials_Fails()
    {
        // Arrange
        var client = DataConnectorHttpTests.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), out _);

        // Act
        var result = await new ElasticsearchDataConnector(client).TestAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private static DataConnectorReadContext Context()
    {
        var connection = DataConnectorTestData.Connection(ElasticsearchDataConnector.Name);
        connection.Put(new HttpConnectionSettings { BaseUrl = "https://search.example.com:9200" });

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Resource = "orders-*";

        return new DataConnectorReadContext(connection, new FakeSecrets(), dataSet) { BatchSize = 500 };
    }
}
