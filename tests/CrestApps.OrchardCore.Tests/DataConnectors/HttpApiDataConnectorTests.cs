using System.Net;
using System.Net.Http;
using System.Text;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class HttpApiDataConnectorTests
{
    [Fact]
    public async Task ReadAsync_WithPageNumbers_ReadsPagesUntilAShortOne()
    {
        // Arrange
        var connector = CreateConnector(request =>
        {
            var page = int.Parse(QueryHelpers.ParseQuery(request.RequestUri.Query)["page"]);

            return StubHttpHandler.Json(page switch
            {
                1 => """{ "data": { "items": [ { "id": 1 }, { "id": 2 } ] } }""",
                2 => """{ "data": { "items": [ { "id": 3 } ] } }""",
                _ => throw new InvalidOperationException("A page after the short one was read."),
            });
        }, out var handler);

        var context = Context(new HttpApiDataSetSettings
        {
            RecordsPath = "data.items",
            Pagination = new HttpPaginationSettings { Type = HttpPaginationType.PageNumber, PageParameter = "page", SizeParameter = "per_page", PageSize = 2 },
        });
        context.Fields.Add("id");

        // Act
        var rows = await ReadAllAsync(connector, context);

        // Assert
        Assert.Equal([1L, 2L, 3L], rows.Select(row => row[0]));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("per_page=2", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_WithACursor_SendsTheCursorBackUntilThereIsNone()
    {
        // Arrange
        var connector = CreateConnector(request =>
        {
            var cursor = QueryHelpers.ParseQuery(request.RequestUri.Query).TryGetValue("after", out var value) ? value.ToString() : null;

            return StubHttpHandler.Json(cursor switch
            {
                null => """{ "items": [ { "id": 1 } ], "next": "abc" }""",
                "abc" => """{ "items": [ { "id": 2 } ], "next": null }""",
                _ => throw new InvalidOperationException("Unexpected cursor."),
            });
        }, out _);

        var context = Context(new HttpApiDataSetSettings
        {
            RecordsPath = "items",
            Pagination = new HttpPaginationSettings { Type = HttpPaginationType.Cursor, CursorParameter = "after", NextPath = "next", SizeParameter = null },
        });
        context.Fields.Add("id");

        // Act
        var rows = await ReadAllAsync(connector, context);

        // Assert
        Assert.Equal([1L, 2L], rows.Select(row => row[0]));
    }

    [Fact]
    public async Task ReadAsync_WithALinkHeader_FollowsRelNext()
    {
        // Arrange
        var connector = CreateConnector(request =>
        {
            var response = StubHttpHandler.Json(request.RequestUri.AbsolutePath == "/items" ? """[ { "id": 1 } ]""" : """[ { "id": 2 } ]""");

            if (request.RequestUri.AbsolutePath == "/items")
            {
                response.Headers.TryAddWithoutValidation("Link", "<https://api.example.com/items/page2>; rel=\"next\", <https://api.example.com/items/last>; rel=\"last\"");
            }

            return response;
        }, out var handler);

        var context = Context(new HttpApiDataSetSettings { Pagination = new HttpPaginationSettings { Type = HttpPaginationType.LinkHeader } });
        context.Fields.Add("id");

        // Act
        var rows = await ReadAllAsync(connector, context);

        // Assert
        Assert.Equal([1L, 2L], rows.Select(row => row[0]));
        Assert.Equal("/items/page2", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task ReadAsync_WithANextLinkToAnotherServer_StopsWithAnError()
    {
        // Arrange
        var connector = CreateConnector(_ => StubHttpHandler.Json("""{ "items": [ { "id": 1 } ], "next": "https://evil.example.net/items" }"""), out _);
        var context = Context(new HttpApiDataSetSettings
        {
            RecordsPath = "items",
            Pagination = new HttpPaginationSettings { Type = HttpPaginationType.NextLink, NextPath = "next" },
        });

        // Act & Assert
        await Assert.ThrowsAsync<DataConnectorException>(() => ReadAllAsync(connector, context));
    }

    [Fact]
    public async Task ReadAsync_StopsAtTheMostPages_ForAnApiThatNeverSaysItIsDone()
    {
        // Arrange
        var connector = CreateConnector(_ => StubHttpHandler.Json("""[ { "id": 1 } ]"""), out var handler);
        var context = Context(new HttpApiDataSetSettings
        {
            Pagination = new HttpPaginationSettings { Type = HttpPaginationType.Offset, SizeParameter = null, MaxPages = 3 },
        });
        context.Fields.Add("id");

        // Act
        var rows = await ReadAllAsync(connector, context);

        // Assert
        Assert.Equal(3, rows.Count);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ReadAsync_WithAPost_RendersThePagingValuesAndParametersInTheBody()
    {
        // Arrange
        var connector = CreateConnector(_ => StubHttpHandler.Json("""{ "rows": [] }"""), out var handler);
        var context = Context(new HttpApiDataSetSettings
        {
            Method = "POST",
            Body = """{ "page": {page}, "since": "{param:Since}" }""",
            RecordsPath = "rows",
            Pagination = new HttpPaginationSettings { Type = HttpPaginationType.PageNumber },
        });
        context.Parameters["Since"] = "2026-01-01";

        // Act
        await ReadAllAsync(connector, context);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{ "page": 1, "since": "2026-01-01" }""", request.Body);
    }

    [Fact]
    public async Task DescribeAsync_FlattensNestedObjects_AndInfersTheirTypes()
    {
        // Arrange
        var connector = CreateConnector(_ => StubHttpHandler.Json("""
            [
              { "id": 1, "price": 2.5, "active": true, "created": "2026-10-01T10:00:00Z", "address": { "city": "Paris" }, "tags": ["a"] },
              { "id": 2, "price": 3, "active": false, "created": "2026-10-02T10:00:00Z", "address": { "city": "Oslo" }, "zip": "00501" }
            ]
            """), out _);

        // Act
        var fields = await connector.DescribeAsync(Context(new HttpApiDataSetSettings()), TestContext.Current.CancellationToken);

        // Assert
        var byName = fields.ToDictionary(field => field.Name);
        Assert.Equal(DataFieldType.Integer, byName["id"].Type);
        Assert.Equal(DataFieldType.Decimal, byName["price"].Type);
        Assert.Equal(DataFieldType.Boolean, byName["active"].Type);
        Assert.Equal(DataFieldType.DateTime, byName["created"].Type);
        Assert.Equal(DataFieldType.Text, byName["address.city"].Type);
        Assert.Equal(DataFieldType.Text, byName["tags"].Type);
        Assert.Equal(DataFieldType.Text, byName["zip"].Type);
    }

    [Fact]
    public async Task ReadAsync_WithACsvResponse_ReadsItThroughTheFileFormat()
    {
        // Arrange
        var connector = CreateConnector(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("id,name\n1,Ada\n2,Grace\n", Encoding.UTF8, "text/csv") }, out _);
        var context = Context(new HttpApiDataSetSettings { Format = "csv" });

        // Act
        var fields = await connector.DescribeAsync(context, TestContext.Current.CancellationToken);
        var rows = await ReadAllAsync(connector, Context(new HttpApiDataSetSettings { Format = "csv" }));

        // Assert
        Assert.Equal(DataFieldType.Integer, fields.Single(field => field.Name == "id").Type);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void NextLink_ReadsRelNextAmongSeveralLinks()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Link", "<https://a.example.com/?page=1>; rel=\"prev\", <https://a.example.com/?page=3>; rel=\"next\"");

        // Act & Assert
        Assert.Equal("https://a.example.com/?page=3", HttpApiDataConnector.NextLink(response));
    }

    [Fact]
    public async Task TestAsync_WhenTheServerRefusesTheCredentials_Fails()
    {
        // Arrange
        var connector = CreateConnector(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), out _);

        // Act
        var result = await connector.TestAsync(Context(new HttpApiDataSetSettings()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("401", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestAsync_WhenTheServerAnswers_Succeeds()
    {
        // Arrange
        var connector = CreateConnector(_ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        // Act
        var result = await connector.TestAsync(Context(new HttpApiDataSetSettings()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
    }

    private static HttpApiDataConnector CreateConnector(Func<HttpRequestMessage, HttpResponseMessage> respond, out StubHttpHandler handler)
    {
        var client = DataConnectorHttpTests.CreateClient(respond, out handler);
        var authenticator = new HttpRequestAuthenticator(new StubHttpClientFactory(handler), new MemoryCache(new MemoryCacheOptions()), new ShellSettings { Name = "Default" });

        return new HttpApiDataConnector(client, authenticator, CreateFileFormats());
    }

    internal static IDataFileFormatManager CreateFileFormats()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddLocalization();
        services.AddDataSourcesCore();

        return services.BuildServiceProvider().GetRequiredService<IDataFileFormatManager>();
    }

    private static DataConnectorReadContext Context(HttpApiDataSetSettings settings)
    {
        var connection = DataConnectorTestData.Connection(HttpApiDataConnector.Name);
        connection.Put(new HttpConnectionSettings { BaseUrl = "https://api.example.com" });

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Resource = "/items";
        dataSet.Put(settings);

        return new DataConnectorReadContext(connection, new FakeSecrets(), dataSet) { BatchSize = 100 };
    }

    private static async Task<List<object[]>> ReadAllAsync(HttpApiDataConnector connector, DataConnectorReadContext context)
    {
        var rows = new List<object[]>();

        await foreach (var batch in connector.ReadAsync(context, TestContext.Current.CancellationToken))
        {
            rows.AddRange(batch.Rows);
        }

        return rows;
    }
}
