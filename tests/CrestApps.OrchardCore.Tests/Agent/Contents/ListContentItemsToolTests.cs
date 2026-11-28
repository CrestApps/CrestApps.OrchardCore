using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// The list tool builds the filter text of the admin Content Items page from its arguments, runs it through the admin
/// list's parser and query service, and returns summaries with paging. The parser here is the one Orchard Core builds
/// for that page, so a filter the page would not understand fails the test.
/// </summary>
public sealed class ListContentItemsToolTests
{
    [Fact]
    public async Task InvokeAsync_WithFilters_ParsesTheAdminFilterTextAndReturnsAPage()
    {
        // Arrange
        ContentOptionsViewModel queriedOptions = null;
        var contentItem = new ContentItem
        {
            ContentItemId = "item-1",
            ContentItemVersionId = "version-1",
            ContentType = "BlogPost",
            DisplayText = "Caf\u00e9 d'\u00e9t\u00e9",
            Latest = true,
            Owner = "user-9",
            Author = "jane",
            ModifiedUtc = new DateTime(2026, 11, 10, 14, 30, 0, DateTimeKind.Utc),
        };

        var query = new Mock<IQuery<ContentItem>>();
        query.Setup(items => items.Skip(20)).Returns(query.Object);
        query.Setup(items => items.Take(20)).Returns(query.Object);
        query.Setup(items => items.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(21);
        query.Setup(items => items.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([contentItem]);

        var services = CreateServices(CreateQueryService(query.Object, options => queriedOptions = options));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemsTool(), services, new()
        {
            ["text"] = "hello",
            ["contentTypes"] = new[] { "blogpost" },
            ["status"] = "draft",
            ["sort"] = "title",
            ["page"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("type:BlogPost status:Draft sort:Title hello", json.GetProperty("filter").GetString());
        Assert.Equal(new[] { "type", "status", "sort", "text" }, queriedOptions.FilterResult.Select(term => term.TermName));
        Assert.Equal(ContentsStatus.Draft, queriedOptions.ContentsStatus);
        Assert.Equal(ContentsOrder.Title, queriedOptions.OrderBy);
        Assert.Equal("BlogPost", queriedOptions.SelectedContentType);

        Assert.Equal(21, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal("UTC", json.GetProperty("timeZone").GetString());

        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("item-1", item.GetProperty("contentItemId").GetString());
        Assert.Equal("version-1", item.GetProperty("contentItemVersionId").GetString());
        Assert.Equal("BlogPost", item.GetProperty("contentType").GetString());
        Assert.Equal("Caf\u00e9 d'\u00e9t\u00e9", item.GetProperty("displayText").GetString());
        Assert.Equal("2026-11-10T14:30:00+00:00", item.GetProperty("modified").GetString());

        // The title reaches the model as written, not as escape sequences.
        Assert.Contains("Caf\u00e9 d'\u00e9t\u00e9", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Re: hello", "type:BlogPost \"Re: hello\"")]
    [InlineData("say \"hi", "type:BlogPost \"say hi\"")]
    [InlineData("status:published", "type:BlogPost \"status:published\"")]
    [InlineData("\"big world\" news", "type:BlogPost \"big world\" news")]
    [InlineData("  ", "type:BlogPost")]
    public async Task InvokeAsync_WithATextTheParserCouldMisread_KeepsTheOtherFilters(string text, string expectedFilter)
    {
        // Arrange
        ContentOptionsViewModel queriedOptions = null;
        var query = new Mock<IQuery<ContentItem>>();
        query.Setup(items => items.Skip(It.IsAny<int>())).Returns(query.Object);
        query.Setup(items => items.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(items => items.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var services = CreateServices(CreateQueryService(query.Object, options => queriedOptions = options));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemsTool(), services, new()
        {
            ["text"] = text,
            ["contentTypes"] = new[] { "BlogPost" },
        });

        // Assert
        Assert.Equal(expectedFilter, AgentToolTestContext.ReadJson(result).GetProperty("filter").GetString());
        Assert.Equal(expectedFilter, queriedOptions.FilterResult.ToString());
        Assert.Contains(queriedOptions.FilterResult, term => term.TermName == "type");
        Assert.Equal(ContentsStatus.Latest, queriedOptions.ContentsStatus);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheParserDoesNotKeepAFilter_ListsNothing()
    {
        // Arrange
        // The site has Content Localization, but this parser has no 'culture' term, as if the term were not registered.
        var queryService = new Mock<IContentsAdminListQueryService>(MockBehavior.Strict);
        var services = CreateServices(queryService.Object, collection => collection.AddSingleton(Mock.Of<IContentLocalizationManager>()));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemsTool(), services, new()
        {
            ["culture"] = "fr",
        });

        // Assert
        Assert.StartsWith("Nothing was listed: the filter could not be read", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("contentTypes", "Nope", "no content type named 'Nope'")]
    [InlineData("culture", "fr", "Content Localization feature")]
    [InlineData("status", "archived", "status must be")]
    [InlineData("sort", "random", "sort must be")]
    [InlineData("stereotype", "two words", "stereotype must be one word")]
    public async Task InvokeAsync_WithAWrongArgument_SaysWhyAndQueriesNothing(string argument, string value, string expected)
    {
        // Arrange
        var queryService = new Mock<IContentsAdminListQueryService>(MockBehavior.Strict);
        var services = CreateServices(queryService.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemsTool(), services, new()
        {
            [argument] = argument == "contentTypes" ? new[] { value } : (object)value,
        });

        // Assert
        Assert.StartsWith("Nothing was listed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
    }

    private static IContentsAdminListQueryService CreateQueryService(IQuery<ContentItem> query, Action<ContentOptionsViewModel> onQuery)
    {
        var queryService = new Mock<IContentsAdminListQueryService>();
        queryService
            .Setup(service => service.QueryAsync(It.IsAny<ContentOptionsViewModel>(), It.IsAny<IUpdateModel>()))
            .ReturnsAsync((ContentOptionsViewModel options, IUpdateModel _) =>
            {
                onQuery(options);

                return query;
            });

        return queryService.Object;
    }

    private static ServiceProvider CreateServices(IContentsAdminListQueryService queryService, Action<IServiceCollection> configure = null)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.LoadAsync(It.IsAny<ContentItem>()))
            .ReturnsAsync((ContentItem contentItem) => contentItem);

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync([new ContentTypeDefinitionBuilder().WithName("BlogPost").Build()]);

        // The parser of the admin Content Items page, built from Orchard Core's own filter provider.
        var builder = new QueryEngineBuilder<ContentItem>();
        new DefaultContentsAdminListFilterProvider().Build(builder);

        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(contentManager.Object);
        services.AddSingleton(contentDefinitionManager.Object);
        services.AddSingleton<IContentsAdminListFilterParser>(new DefaultContentsAdminListFilterParser(builder.Build()));
        services.AddSingleton(queryService);
        services.AddSingleton(Options.Create(new PagerOptions()));
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
