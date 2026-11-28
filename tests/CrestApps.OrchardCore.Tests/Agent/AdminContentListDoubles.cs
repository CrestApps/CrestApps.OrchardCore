using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Stands in for the list of the admin Content Items page in a tool test: the filter parser is the one Orchard Core
/// builds for that page, and the query service returns the content items of the test.
/// </summary>
internal static class AdminContentListDoubles
{
    /// <summary>
    /// Adds the parser and the query service of the admin Content Items page to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    /// <param name="contentManager">The content manager double, which loads the listed content items.</param>
    /// <param name="onQuery">Receives the options the tool asked the list for.</param>
    /// <param name="contentItems">The content items the list returns.</param>
    public static void AddAdminContentList(
        this IServiceCollection services,
        Mock<IContentManager> contentManager,
        Action<ContentOptionsViewModel> onQuery,
        params ContentItem[] contentItems)
    {
        var query = new Mock<IQuery<ContentItem>>();
        query.Setup(items => items.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(items => items.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(contentItems);

        var queryService = new Mock<IContentsAdminListQueryService>();
        queryService
            .Setup(service => service.QueryAsync(It.IsAny<ContentOptionsViewModel>(), It.IsAny<IUpdateModel>()))
            .ReturnsAsync((ContentOptionsViewModel options, IUpdateModel _) =>
            {
                onQuery?.Invoke(options);

                return query.Object;
            });

        var builder = new QueryEngineBuilder<ContentItem>();
        new DefaultContentsAdminListFilterProvider().Build(builder);

        contentManager
            .Setup(manager => manager.LoadAsync(It.IsAny<ContentItem>()))
            .ReturnsAsync((ContentItem contentItem) => contentItem);

        services.AddSingleton<IContentsAdminListFilterParser>(new DefaultContentsAdminListFilterParser(builder.Build()));
        services.AddSingleton(queryService.Object);
    }
}
