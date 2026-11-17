using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Sitemaps;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Sitemaps.Cache;
using OrchardCore.Sitemaps.Models;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Sitemaps;

/// <summary>
/// The tools that list, save and delete sitemaps and clear the sitemap cache. They go through the sitemap manager, the
/// path check and the cache provider the Sitemaps admin pages use, and a refused save leaves the sitemap as it was.
/// </summary>
public sealed class SitemapToolsTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc);

    private static readonly DateTime _earlier = new(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListSitemaps_ReturnsTheSitemapsWithTheirSourcesAndTheCachedFiles()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSitemapsTool(), harness.Services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { "index-1", "main-1" }, items.Select(item => item.GetProperty("sitemapId").GetString()));
        Assert.Equal(new[] { "Main_abc.xml", "Old_def.xml" }, json.GetProperty("cachedFiles").EnumerateArray().Select(file => file.GetString()));

        var index = items[0];

        Assert.Equal("sitemapIndex", index.GetProperty("kind").GetString());
        Assert.Equal("containedSitemaps", Assert.Single(index.GetProperty("sources").EnumerateArray()).GetProperty("type").GetString());

        var main = items[1];
        var sources = main.GetProperty("sources").EnumerateArray().ToList();

        Assert.Equal("sitemap", main.GetProperty("kind").GetString());
        Assert.Equal("Main", main.GetProperty("name").GetString());
        Assert.Equal("sitemap.xml", main.GetProperty("path").GetString());
        Assert.True(main.GetProperty("enabled").GetBoolean());

        Assert.Equal("src-1", sources[0].GetProperty("sourceId").GetString());
        Assert.Equal("contentTypes", sources[0].GetProperty("type").GetString());
        Assert.True(sources[0].GetProperty("indexAll").GetBoolean());
        Assert.Equal("weekly", sources[0].GetProperty("changeFrequency").GetString());
        Assert.Equal(5, sources[0].GetProperty("priority").GetInt32());

        Assert.Equal("customPath", sources[1].GetProperty("type").GetString());
        Assert.Equal("pricing", sources[1].GetProperty("path").GetString());
        Assert.Equal("monthly", sources[1].GetProperty("changeFrequency").GetString());
    }

    [Fact]
    public async Task ListSitemaps_WithASearch_KeepsTheSitemapsWhoseNameHasTheText()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSitemapsTool(), harness.Services, new()
        {
            ["search"] = "mai",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("main-1", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("sitemapId").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SaveSitemap_WithoutAnId_CreatesASitemapWithItsSources()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["name"] = "Blog Posts",
            ["sources"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "contentTypes",
                    ["contentTypes"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["contentType"] = "blogpost",
                            ["changeFrequency"] = "daily",
                            ["priority"] = 7,
                        },
                        new JsonObject
                        {
                            ["contentType"] = "Article",
                        },
                    },
                },
                new JsonObject
                {
                    ["type"] = "customPath",
                    ["path"] = "/contact",
                    ["changeFrequency"] = "yearly",
                    ["priority"] = 3,
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("blog-posts.xml", json.GetProperty("sitemap").GetProperty("path").GetString());

        var sitemap = Assert.IsType<Sitemap>(Assert.Single(harness.SitemapManager.Updated));

        Assert.Equal("new-1", sitemap.SitemapId);
        Assert.Equal("Blog Posts", sitemap.Name);
        Assert.Equal("blog-posts.xml", sitemap.Path);
        Assert.True(sitemap.Enabled);

        var contentTypes = Assert.IsType<ContentTypesSitemapSource>(sitemap.SitemapSources[0]);

        // A list of content types is meant to be used, so the source no longer lists every content type.
        Assert.False(contentTypes.IndexAll);
        Assert.False(contentTypes.LimitItems);
        Assert.Equal(new[] { "BlogPost", "Article" }, contentTypes.ContentTypes.Select(entry => entry.ContentTypeName));
        Assert.Equal(ChangeFrequency.Daily, contentTypes.ContentTypes[0].ChangeFrequency);
        Assert.Equal(7, contentTypes.ContentTypes[0].Priority);
        Assert.Equal(5, contentTypes.ContentTypes[1].Priority);
        Assert.False(string.IsNullOrEmpty(contentTypes.Id));

        var customPath = Assert.IsType<CustomPathSitemapSource>(sitemap.SitemapSources[1]);

        Assert.Equal("contact", customPath.Path);
        Assert.Equal(ChangeFrequency.Yearly, customPath.ChangeFrequency);
        Assert.Equal(3, customPath.Priority);
        Assert.Equal(_now, customPath.LastUpdate);
    }

    [Fact]
    public async Task SaveSitemap_WithSources_ReplacesTheListAndKeepsTheSourcesNamedByTheirId()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "main-1",
            ["sources"] = """[{ "sourceId": "src-2", "type": "customPath", "priority": 9 }, { "type": "customPath", "path": "docs/getting-started" }]""",
        });

        // Assert
        Assert.False(AgentToolTestContext.ReadJson(result).GetProperty("created").GetBoolean());

        var sitemap = Assert.Single(harness.SitemapManager.Updated);
        var kept = Assert.IsType<CustomPathSitemapSource>(sitemap.SitemapSources[0]);
        var added = Assert.IsType<CustomPathSitemapSource>(sitemap.SitemapSources[1]);

        Assert.Equal(2, sitemap.SitemapSources.Count);
        Assert.Equal("src-2", kept.Id);
        Assert.Equal("pricing", kept.Path);
        Assert.Equal(ChangeFrequency.Monthly, kept.ChangeFrequency);
        Assert.Equal(9, kept.Priority);
        Assert.Equal(_now, kept.LastUpdate);
        Assert.Equal("docs/getting-started", added.Path);
        Assert.Equal("Main", sitemap.Name);
        Assert.Equal("sitemap.xml", sitemap.Path);
    }

    [Fact]
    public async Task SaveSitemap_WhenASourceIsListedUnchanged_KeepsItsLastUpdate()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "main-1",
            ["enabled"] = false,
            ["sources"] = new JsonArray
            {
                new JsonObject
                {
                    ["sourceId"] = "src-1",
                    ["type"] = "contentTypes",
                },
                new JsonObject
                {
                    ["sourceId"] = "src-2",
                    ["type"] = "customPath",
                    ["path"] = "pricing",
                },
            },
        });

        // Assert
        var sitemap = Assert.Single(harness.SitemapManager.Updated);

        Assert.False(sitemap.Enabled);
        Assert.True(Assert.IsType<ContentTypesSitemapSource>(sitemap.SitemapSources[0]).IndexAll);
        Assert.Equal(_earlier, Assert.IsType<CustomPathSitemapSource>(sitemap.SitemapSources[1]).LastUpdate);
    }

    [Theory]
    [InlineData("""{ "path": "blog" }""", "the path 'blog' cannot be used")]
    [InlineData("""{ "path": "index.xml" }""", "the path 'index.xml' cannot be used")]
    [InlineData("""{ "sources": [{ "type": "contentTypes", "contentTypes": [{ "contentType": "Widget" }] }] }""", "'Widget' is not a content type that has an address. The content types that can be listed are: Article, BlogPost.")]
    [InlineData("""{ "sources": [{ "type": "customPath", "path": "pricing", "frequency": "daily" }] }""", "source 1 has properties it cannot take: frequency")]
    [InlineData("""{ "sources": [{ "type": "customPath", "path": "a b" }] }""", "the path 'a b' cannot be used")]
    [InlineData("""{ "sources": [{ "type": "customPath" }] }""", "a new custom path source needs a path")]
    [InlineData("""{ "sources": [{ "type": "contentTypes", "priority": 11 }] }""", "priority must be a whole number from 0 to 10")]
    [InlineData("""{ "sources": [{ "type": "contentTypes", "changeFrequency": "sometimes" }] }""", "changeFrequency must be always, hourly")]
    [InlineData("""{ "sources": [{ "type": "feed" }] }""", "source 1 needs a type")]
    [InlineData("""{ "sources": [{ "sourceId": "nope", "type": "customPath" }] }""", "has no source with the ID 'nope'")]
    [InlineData("""{ "sources": [{ "sourceId": "src-1", "type": "customPath" }] }""", "is not a customPath source")]
    [InlineData("""{ "sources": "pricing" }""", "sources must be a JSON array")]
    [InlineData("""{ }""", "give name, path, enabled or sources")]
    public async Task SaveSitemap_WhenAnArgumentIsNotValid_LeavesTheSitemapAsItWas(string arguments, string expected)
    {
        // Arrange
        var harness = new Harness();
        var toolArguments = new Dictionary<string, object>
        {
            ["sitemapId"] = "main-1",
        };

        foreach (var argument in JsonNode.Parse(arguments).AsObject())
        {
            // A text argument reaches a tool as a string; a conditional expression would turn it into a JSON value.
            if (argument.Value is JsonValue value && value.TryGetValue<string>(out var text))
            {
                toolArguments[argument.Key] = text;
            }
            else
            {
                toolArguments[argument.Key] = argument.Value.DeepClone();
            }
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, toolArguments);

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.SitemapManager.Updated);

        var sitemap = harness.SitemapManager.Sitemaps["main-1"];
        var customPath = Assert.IsType<CustomPathSitemapSource>(sitemap.SitemapSources[1]);

        Assert.Equal("sitemap.xml", sitemap.Path);
        Assert.Equal(2, sitemap.SitemapSources.Count);
        Assert.Equal("pricing", customPath.Path);
        Assert.Equal(5, customPath.Priority);
        Assert.Equal(_earlier, customPath.LastUpdate);
    }

    [Fact]
    public async Task SaveSitemap_WithoutANameOrAnId_CreatesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["enabled"] = true,
        });

        // Assert
        Assert.Equal("Nothing was saved: give name to create a sitemap, or sitemapId to update one.", result);
        Assert.Empty(harness.SitemapManager.Updated);
    }

    [Fact]
    public async Task SaveSitemap_ForASitemapIndex_ChangesItsStateButNotWhatItContains()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var renamed = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "index-1",
            ["name"] = "All sitemaps",
            ["enabled"] = false,
        });

        var refused = await AgentToolTestContext.InvokeAsync(new SaveSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "index-1",
            ["sources"] = new JsonArray(),
        });

        // Assert
        Assert.Equal("sitemapIndex", AgentToolTestContext.ReadJson(renamed).GetProperty("sitemap").GetProperty("kind").GetString());
        Assert.StartsWith("Nothing was saved: this is a sitemap index.", refused, StringComparison.Ordinal);

        var index = Assert.Single(harness.SitemapManager.Updated);

        Assert.Equal("All sitemaps", index.Name);
        Assert.False(index.Enabled);
        Assert.Single(index.SitemapSources);
    }

    [Fact]
    public async Task DeleteSitemap_DeletesTheSitemapThroughTheSitemapManager()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "main-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("Main", json.GetProperty("name").GetString());
        Assert.Equal("sitemap", json.GetProperty("kind").GetString());
        Assert.Equal("main-1", Assert.Single(harness.SitemapManager.Deleted));
        Assert.False(harness.SitemapManager.Sitemaps.ContainsKey("main-1"));
    }

    [Fact]
    public async Task DeleteSitemap_WhenThereIsNoSuchSitemap_DeletesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSitemapTool(), harness.Services, new()
        {
            ["sitemapId"] = "nope",
        });

        // Assert
        Assert.StartsWith("Nothing was deleted: there is no sitemap with the ID 'nope'.", result, StringComparison.Ordinal);
        Assert.Empty(harness.SitemapManager.Deleted);
    }

    [Fact]
    public async Task PurgeSitemapCache_WithoutAFile_ClearsEveryCachedFile()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeSitemapCacheTool(), harness.Services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("purged").GetBoolean());
        Assert.True(json.GetProperty("all").GetBoolean());
        Assert.Equal(2, json.GetProperty("purgedFiles").GetInt32());
        harness.CacheProvider.Verify(provider => provider.PurgeAllAsync(), Times.Once);
        harness.CacheProvider.Verify(provider => provider.PurgeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PurgeSitemapCache_WithAFile_ClearsThatCachedFile()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeSitemapCacheTool(), harness.Services, new()
        {
            ["cacheFileName"] = "main_abc.xml",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("all").GetBoolean());
        Assert.Equal("Main_abc.xml", json.GetProperty("cacheFileName").GetString());
        harness.CacheProvider.Verify(provider => provider.PurgeAsync("Main_abc.xml"), Times.Once);
        harness.CacheProvider.Verify(provider => provider.PurgeAllAsync(), Times.Never);
    }

    [Theory]
    [InlineData("nope.xml")]
    [InlineData("../web.config")]
    public async Task PurgeSitemapCache_WithAFileTheCacheDoesNotList_ClearsNothing(string cacheFileName)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeSitemapCacheTool(), harness.Services, new()
        {
            ["cacheFileName"] = cacheFileName,
        });

        // Assert
        Assert.StartsWith("Nothing was cleared", result, StringComparison.Ordinal);
        harness.CacheProvider.Verify(provider => provider.PurgeAsync(It.IsAny<string>()), Times.Never);
        harness.CacheProvider.Verify(provider => provider.PurgeAllAsync(), Times.Never);
    }

    [Fact]
    public async Task PurgeSitemapCache_WhenSomeFilesCouldNotBeRemoved_SaysSo()
    {
        // Arrange
        var harness = new Harness();
        harness.CacheProvider.Setup(provider => provider.PurgeAllAsync()).ReturnsAsync(true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeSitemapCacheTool(), harness.Services);

        // Assert
        Assert.StartsWith("The sitemap cache was cleared, but some files could not be removed.", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// A site with one sitemap, which lists every content type and one custom path, and one sitemap index.
    /// </summary>
    private sealed class Harness
    {
        public Harness()
        {
            SitemapManager.Sitemaps["main-1"] = new Sitemap
            {
                SitemapId = "main-1",
                Name = "Main",
                Path = "sitemap.xml",
                SitemapSources =
                [
                    new ContentTypesSitemapSource
                    {
                        Id = "src-1",
                        ChangeFrequency = ChangeFrequency.Weekly,
                    },
                    new CustomPathSitemapSource
                    {
                        Id = "src-2",
                        Path = "pricing",
                        ChangeFrequency = ChangeFrequency.Monthly,
                        LastUpdate = _earlier,
                    },
                ],
            };

            SitemapManager.Sitemaps["index-1"] = new TestSitemapIndex
            {
                SitemapId = "index-1",
                Name = "Index",
                Path = "index.xml",
                SitemapSources =
                [
                    new TestSitemapIndexSource
                    {
                        Id = "src-9",
                    },
                ],
            };

            var idGenerator = new SequentialIdGenerator();

            var coordinator = new Mock<IRouteableContentTypeCoordinator>();
            coordinator
                .Setup(service => service.ListRoutableTypeDefinitionsAsync())
                .ReturnsAsync(
                [
                    new ContentTypeDefinitionBuilder().WithName("BlogPost").Build(),
                    new ContentTypeDefinitionBuilder().WithName("Article").Build(),
                ]);

            var clock = new Mock<IClock>();
            clock.Setup(service => service.UtcNow).Returns(_now);

            CacheProvider.Setup(provider => provider.ListAsync()).ReturnsAsync(["Main_abc.xml", "Old_def.xml"]);

            var services = AgentToolTestContext.CreateServices();

            services.AddSingleton<ISitemapManager>(SitemapManager);
            services.AddSingleton<ISitemapIdGenerator>(idGenerator);
            services.AddSingleton<ISitemapHelperService>(new TestSitemapHelperService(SitemapManager));
            services.AddSingleton<ISitemapSourceFactory>(new SitemapSourceFactory<ContentTypesSitemapSource>(idGenerator));
            services.AddSingleton<ISitemapSourceFactory>(new SitemapSourceFactory<CustomPathSitemapSource>(idGenerator));
            services.AddSingleton(coordinator.Object);
            services.AddSingleton(clock.Object);
            services.AddSingleton(CacheProvider.Object);

            Services = services.BuildServiceProvider();
        }

        public InMemorySitemapManager SitemapManager { get; } = new();

        public Mock<ISitemapCacheProvider> CacheProvider { get; } = new();

        public ServiceProvider Services { get; }
    }

    /// <summary>
    /// Stands in for the sitemap index of the Sitemaps module, which is any sitemap type that is not a sitemap.
    /// </summary>
    private sealed class TestSitemapIndex : SitemapType;

    private sealed class TestSitemapIndexSource : SitemapSource;

    private sealed class SequentialIdGenerator : ISitemapIdGenerator
    {
        private int _count;

        public string GenerateUniqueId()
            => $"new-{++_count}";
    }

    private sealed class InMemorySitemapManager : ISitemapManager
    {
        public Dictionary<string, SitemapType> Sitemaps { get; } = [];

        public List<SitemapType> Updated { get; } = [];

        public List<string> Deleted { get; } = [];

        public Task<string> GetIdentifierAsync()
            => Task.FromResult("identifier");

        public Task<IEnumerable<SitemapType>> LoadSitemapsAsync()
            => GetSitemapsAsync();

        public Task<IEnumerable<SitemapType>> GetSitemapsAsync()
            => Task.FromResult<IEnumerable<SitemapType>>(Sitemaps.Values.ToArray());

        public Task<SitemapType> LoadSitemapAsync(string sitemapId)
            => GetSitemapAsync(sitemapId);

        public Task<SitemapType> GetSitemapAsync(string sitemapId)
            => Task.FromResult(Sitemaps.GetValueOrDefault(sitemapId));

        public Task DeleteSitemapAsync(string sitemapId)
        {
            Sitemaps.Remove(sitemapId);
            Deleted.Add(sitemapId);

            return Task.CompletedTask;
        }

        public Task UpdateSitemapAsync(SitemapType sitemap)
        {
            Sitemaps[sitemap.SitemapId] = sitemap;
            Updated.Add(sitemap);

            return Task.CompletedTask;
        }

        public Task UpdateSitemapAsync()
            => Task.CompletedTask;
    }

    /// <summary>
    /// The path rules of the Sitemaps module that the tests use: the .xml ending and one sitemap for a path.
    /// </summary>
    private sealed class TestSitemapHelperService : ISitemapHelperService
    {
        private readonly InMemorySitemapManager _sitemapManager;

        public TestSitemapHelperService(InMemorySitemapManager sitemapManager)
        {
            _sitemapManager = sitemapManager;
        }

        public Task ValidatePathAsync(string path, IUpdateModel updater, string sitemapId = null)
        {
            if (!path.EndsWith(Sitemap.PathExtension, StringComparison.Ordinal))
            {
                updater.ModelState.AddModelError("Path", "Your permalink must end with .xml.");
            }

            if (_sitemapManager.Sitemaps.Values.Any(sitemap => sitemap.SitemapId != sitemapId && string.Equals(sitemap.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                updater.ModelState.AddModelError("Path", "Your permalink is already in use.");
            }

            return Task.CompletedTask;
        }

        public string GetSitemapSlug(string name)
            => name.ToLowerInvariant().Replace(' ', '-') + Sitemap.PathExtension;
    }
}
