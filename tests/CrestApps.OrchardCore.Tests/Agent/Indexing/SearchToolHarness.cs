using CrestApps.OrchardCore.Tests.Agent.Queries;
using CrestApps.OrchardCore.Tests.Agent.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Core.Models;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Lucene.Models;
using OrchardCore.Queries;
using OrchardCore.Search.Abstractions;
using OrchardCore.Search.Models;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.Indexing;

/// <summary>
/// Builds the services the index, search and query tools use, with doubles that record what a tool did: an index
/// profile manager with a Lucene, an Elasticsearch and an Azure AI Search index, the index managers of the first two
/// providers, a query manager, a search service for Lucene, a content store and the site search settings.
/// </summary>
internal sealed class SearchToolHarness
{
    /// <summary>
    /// The content types the content definition knows.
    /// </summary>
    public static readonly string[] ContentTypes = ["BlogPost", "Article", "Page"];

    public SearchToolHarness()
    {
        Blog = AddContentIndex("blog-1", "blog", "Lucene", ["BlogPost"]);
        Blog.CreatedUtc = new DateTime(2026, 11, 2, 8, 15, 0, DateTimeKind.Utc);
        Blog.Author = "admin";
        Blog.Put(new LuceneIndexMetadata
        {
            AnalyzerName = "standardanalyzer",
            StoreSourceData = true,
            IndexMappings = new LuceneIndexMap
            {
                KeyFieldName = "ContentItemId",
                Fields = ["Content.ContentItem.FullText", "Content.ContentItem.DisplayText", "ContentItemId"],
            },
        });
        Blog.Put(new LuceneIndexDefaultQueryMetadata
        {
            DefaultSearchFields = ["Content.ContentItem.FullText"],
            AllowLuceneQueries = true,
        });

        Products = AddContentIndex("products-1", "products", "Elasticsearch", ["Page"]);
        Azure = AddContentIndex("azure-1", "site", "AzureAISearch", ["Page"]);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a user is signed in.
    /// </summary>
    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants or refuses a permission on a resource. The default grants everything.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Gets the Lucene index profile of the site.
    /// </summary>
    public IndexProfile Blog { get; }

    /// <summary>
    /// Gets the Elasticsearch index profile of the site.
    /// </summary>
    public IndexProfile Products { get; }

    /// <summary>
    /// Gets the Azure AI Search index profile of the site.
    /// </summary>
    public IndexProfile Azure { get; }

    public FakeIndexProfileManager IndexProfiles { get; } = new();

    public FakeIndexManager LuceneIndexes { get; } = new();

    public FakeIndexManager ElasticsearchIndexes { get; } = new();

    public FakeDistributedLock Lock { get; } = new();

    public FakeQueryManager Queries { get; } = new();

    public FakeSearchService LuceneSearch { get; } = new();

    /// <summary>
    /// Gets the content items of the content store.
    /// </summary>
    public List<ContentItem> ContentItems { get; } = [];

    /// <summary>
    /// Gets or sets the default index of the site search settings.
    /// </summary>
    public string DefaultSearchIndex { get; set; }

    /// <summary>
    /// Gets or sets the address of Elasticsearch in the configuration. Without one, Elasticsearch is not configured.
    /// </summary>
    public string ElasticsearchUrl { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <returns>The service provider.</returns>
    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, (permission, resource) => Authorize(permission, resource));

        services.AddSingleton<IIndexProfileManager>(IndexProfiles);
        services.AddKeyedSingleton<IIndexManager>("Lucene", LuceneIndexes);
        services.AddKeyedSingleton<IIndexManager>("Elasticsearch", ElasticsearchIndexes);
        services.AddSingleton<IDistributedLock>(Lock);
        services.AddSingleton<IQueryManager>(Queries);
        services.AddKeyedSingleton<ISearchService>("Lucene", LuceneSearch);

        foreach (var source in Queries.Sources)
        {
            services.AddSingleton<IQuerySource>(new FakeQuerySource(source));
        }

        var indexingOptions = new IndexingOptions();

        indexingOptions.AddIndexingSource("Lucene", IndexingConstants.ContentsIndexSource, entry => entry.DisplayName = new LocalizedString("Content in Lucene", "Content in Lucene"));
        indexingOptions.AddIndexingSource("Elasticsearch", IndexingConstants.ContentsIndexSource, entry => entry.DisplayName = new LocalizedString("Content in Elasticsearch", "Content in Elasticsearch"));

        services.AddSingleton(Options.Create(indexingOptions));
        services.AddSingleton(Options.Create(new ElasticsearchConnectionOptions
        {
            Url = ElasticsearchUrl,
        }));

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();

        contentDefinitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => ContentTypes.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? new ContentTypeDefinition(name, name)
                : null);

        services.AddSingleton(contentDefinitionManager.Object);

        var contentManager = new Mock<IContentManager>();

        // The content store returns items in its own order, not in the order of the IDs.
        contentManager
            .Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => ContentItems
                .Where(contentItem => ids.Contains(contentItem.ContentItemId))
                .OrderBy(contentItem => contentItem.ContentItemId, StringComparer.Ordinal)
                .ToList());

        services.AddSingleton(contentManager.Object);

        // The site has search settings only when a default index was chosen on the Site Search settings page.
        var searchSettings = new SearchSettings
        {
            DefaultIndexProfileName = DefaultSearchIndex,
        };

        var site = new Mock<ISite>();
        site.Setup(settings => settings.TryGet(out searchSettings)).Returns(DefaultSearchIndex is not null);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        services.AddSingleton(siteService.Object);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Asserts that a refused call changed no index profile, no physical index and no saved query, and ran no query
    /// and no search.
    /// </summary>
    public void VerifyNothingChangedOrRan()
    {
        Assert.Empty(IndexProfiles.Calls);
        Assert.Empty(LuceneIndexes.Calls);
        Assert.Empty(ElasticsearchIndexes.Calls);
        Assert.Empty(Lock.Keys);
        Assert.Empty(Queries.Calls);
        Assert.Empty(LuceneSearch.Searches);
    }

    private IndexProfile AddContentIndex(string id, string name, string providerName, string[] contentTypes)
    {
        var indexProfile = new IndexProfile
        {
            Id = id,
            Name = name,
            ProviderName = providerName,
            Type = IndexingConstants.ContentsIndexSource,
            IndexName = name,
            IndexFullName = $"tenant_{name}",
        };

        indexProfile.Put(new ContentIndexMetadata
        {
            IndexedContentTypes = contentTypes,
            Culture = "any",
        });

        IndexProfiles.Profiles.Add(indexProfile);

        return indexProfile;
    }
}
