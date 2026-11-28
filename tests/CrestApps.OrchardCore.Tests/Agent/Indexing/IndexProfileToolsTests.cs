using CrestApps.OrchardCore.AI.Agent.Indexing;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.AI;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Indexing;

/// <summary>
/// The tools that list, read, save, synchronize, rebuild and delete search index profiles. They call the index profile
/// manager and the index manager of the provider in the order the Indexes admin page does, a refused call changes
/// nothing, and the tools that start indexing say that it was started, not that it is finished.
/// </summary>
public sealed class IndexProfileToolsTests
{
    [Fact]
    public async Task ListIndexProfiles_ReturnsTheProfilesAndTheSourcesANewOneCanBeCreatedFor()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListIndexProfilesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var sources = json.GetProperty("sources").EnumerateArray().ToList();

        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { "blog", "products", "site" }, items.Select(item => item.GetProperty("name").GetString()));

        var blog = items[0];

        Assert.Equal("blog-1", blog.GetProperty("id").GetString());
        Assert.Equal("Lucene", blog.GetProperty("provider").GetString());
        Assert.Equal("Content", blog.GetProperty("type").GetString());
        Assert.Equal("blog", blog.GetProperty("indexName").GetString());
        Assert.Equal(new[] { "BlogPost" }, blog.GetProperty("indexedContentTypes").EnumerateArray().Select(type => type.GetString()));
        Assert.Equal("2026-11-02T08:15:00+00:00", blog.GetProperty("created").GetString());
        Assert.Equal("UTC", json.GetProperty("timeZone").GetString());

        Assert.Equal(new[] { "Elasticsearch", "Lucene" }, sources.Select(source => source.GetProperty("provider").GetString()));
        Assert.Equal("Content", sources[1].GetProperty("type").GetString());
        Assert.Equal("Content in Lucene", sources[1].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ListIndexProfiles_WithAProviderAndASearch_KeepsTheMatchingProfilesOfThatProvider()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListIndexProfilesTool(), services, new()
        {
            ["provider"] = "Lucene",
            ["search"] = "BLO",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal("blog-1", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetIndexProfile_ByName_ReturnsTheSettingsAndTheIndexedFields()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetIndexProfileTool(), services, new()
        {
            ["index"] = "blog",
        });

        // Assert
        var indexProfile = AgentToolTestContext.ReadJson(result).GetProperty("indexProfile");
        var content = indexProfile.GetProperty("content");
        var providerSettings = indexProfile.GetProperty("providerSettings");

        Assert.Equal("blog-1", indexProfile.GetProperty("id").GetString());
        Assert.Equal("tenant_blog", indexProfile.GetProperty("indexFullName").GetString());
        Assert.Equal("admin", indexProfile.GetProperty("author").GetString());
        Assert.Equal(new[] { "BlogPost" }, content.GetProperty("indexedContentTypes").EnumerateArray().Select(type => type.GetString()));
        Assert.Equal("any", content.GetProperty("culture").GetString());
        Assert.False(content.GetProperty("indexLatest").GetBoolean());
        Assert.Equal("standardanalyzer", providerSettings.GetProperty("analyzer").GetString());
        Assert.True(providerSettings.GetProperty("storeSourceData").GetBoolean());
        Assert.True(providerSettings.GetProperty("allowLuceneQueries").GetBoolean());
        Assert.Equal(new[] { "Content.ContentItem.FullText" }, providerSettings.GetProperty("defaultSearchFields").EnumerateArray().Select(field => field.GetString()));
        Assert.Equal(
            new[] { "Content.ContentItem.DisplayText", "Content.ContentItem.FullText", "ContentItemId" },
            indexProfile.GetProperty("fields").EnumerateArray().Select(field => field.GetString()));
        Assert.Equal(3, indexProfile.GetProperty("fieldsTotal").GetInt32());
    }

    [Fact]
    public async Task GetIndexProfile_ForAnElasticsearchIndex_ReturnsItsSettingsAndTheFieldPathsOfItsMapping()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Products.Put(new ElasticsearchIndexMetadata
        {
            AnalyzerName = "standard",
            IndexMappings = new ElasticsearchIndexMap
            {
                KeyFieldName = "ContentItemId",
                Mapping = new TypeMapping
                {
                    Properties = new Properties
                    {
                        { "ContentItemId", new KeywordProperty() },
                        {
                            "Content", new ObjectProperty
                            {
                                Properties = new Properties
                                {
                                    { "DisplayText", new TextProperty() },
                                },
                            }
                        },
                    },
                },
            },
        });
        harness.Products.Put(new ElasticsearchDefaultQueryMetadata
        {
            DefaultSearchFields = ["Content.DisplayText"],
            SearchType = "query_string",
        });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetIndexProfileTool(), services, new()
        {
            ["index"] = "products",
        });

        // Assert
        var indexProfile = AgentToolTestContext.ReadJson(result).GetProperty("indexProfile");
        var providerSettings = indexProfile.GetProperty("providerSettings");

        Assert.Equal("Elasticsearch", indexProfile.GetProperty("provider").GetString());
        Assert.Equal("standard", providerSettings.GetProperty("analyzer").GetString());
        Assert.True(providerSettings.GetProperty("storeSourceData").GetBoolean());
        Assert.Equal("query_string", providerSettings.GetProperty("searchType").GetString());
        Assert.Equal(new[] { "Content.DisplayText" }, providerSettings.GetProperty("defaultSearchFields").EnumerateArray().Select(field => field.GetString()));

        // The fields of a nested object are returned as paths.
        Assert.Equal(
            new[] { "Content.DisplayText", "ContentItemId" },
            indexProfile.GetProperty("fields").EnumerateArray().Select(field => field.GetString()));
    }

    [Fact]
    public async Task GetIndexProfile_ForAProviderWhoseSettingsAreNotDescribed_ReturnsTheContentSettingsOnly()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetIndexProfileTool(), services, new()
        {
            ["index"] = "azure-1",
        });

        // Assert
        var indexProfile = AgentToolTestContext.ReadJson(result).GetProperty("indexProfile");

        Assert.Equal("site", indexProfile.GetProperty("name").GetString());
        Assert.Equal("AzureAISearch", indexProfile.GetProperty("provider").GetString());
        Assert.True(indexProfile.TryGetProperty("content", out _));
        Assert.False(indexProfile.TryGetProperty("providerSettings", out _));
        Assert.False(indexProfile.TryGetProperty("fields", out _));
    }

    [Fact]
    public async Task GetIndexProfile_WhenThereIsNone_SaysSo()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetIndexProfileTool(), services, new()
        {
            ["index"] = "missing",
        });

        // Assert
        Assert.StartsWith("There is no index profile with the ID or the name 'missing'.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveIndexProfile_ForANewName_CreatesTheProfileAndItsIndexAndStartsIndexing()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "news",
            ["providerName"] = "lucene",
            ["type"] = "Content",
            ["indexName"] = "news",
            ["indexedContentTypes"] = new[] { "Article", "BlogPost" },
            ["culture"] = "en-US",
            ["indexLatest"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = harness.IndexProfiles.Profiles.Single(profile => profile.Name == "news");
        var metadata = created.GetOrCreate<ContentIndexMetadata>();

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Contains("starts when this reply ends", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal("Lucene", created.ProviderName);
        Assert.Equal("news", created.IndexName);
        Assert.Equal(new[] { "Article", "BlogPost" }, metadata.IndexedContentTypes);
        Assert.Equal("en-US", metadata.Culture);
        Assert.True(metadata.IndexLatest);

        // The order of the Create action of the Indexes page: validate, store, create the physical index, synchronize.
        Assert.Same(created, Assert.Single(harness.IndexProfiles.Validated));
        // The synchronization is queued for when the message ends (IndexProfileSynchronization), not run as an HTTP background job.
        Assert.Equal(new[] { "Create:news" }, harness.IndexProfiles.Calls);
        Assert.Equal(new[] { "Create:news" }, harness.LuceneIndexes.Calls);
    }

    [Fact]
    public async Task SaveIndexProfile_WhenTheProviderCannotCreateTheIndex_RemovesTheProfileAgain()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.LuceneIndexes.Creates = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "news",
            ["providerName"] = "Lucene",
            ["type"] = "Content",
            ["indexName"] = "news",
            ["indexedContentTypes"] = new[] { "Article" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the provider 'Lucene' could not create the index 'news'.", result, StringComparison.Ordinal);
        Assert.Equal(new[] { "Create:news", "Delete:news" }, harness.IndexProfiles.Calls);
        Assert.DoesNotContain(harness.IndexProfiles.Profiles, profile => profile.Name == "news");
    }

    [Theory]
    [InlineData("Lucene", "Content", "news", "BlogPost", "Unknown", "there is no content type named 'Unknown'")]
    [InlineData("Lucene", "Content", "blog", "BlogPost", null, "the provider 'Lucene' already has an index named 'blog'")]
    [InlineData("Solr", "Content", "news", "BlogPost", null, "the site has no search provider 'Solr' with the index type 'Content'")]
    [InlineData("Lucene", "Content", "news", null, null, "At least one content type must be selected.")]
    public async Task SaveIndexProfile_WhenANewProfileIsNotValid_SavesNothing(string providerName, string type, string indexName, string contentType, string secondContentType, string expected)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["name"] = "news",
            ["providerName"] = providerName,
            ["type"] = type,
            ["indexName"] = indexName,
        };

        if (contentType is not null)
        {
            arguments["indexedContentTypes"] = secondContentType is null
                ? new[] { contentType }
                : new[] { contentType, secondContentType };
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved: ", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task SaveIndexProfile_ForAnExistingName_ReplacesTheContentSettingsAfterValidatingACopy()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "blog",
            ["indexedContentTypes"] = new[] { "BlogPost", "Article" },
            ["indexLatest"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var metadata = harness.Blog.GetOrCreate<ContentIndexMetadata>();

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Contains("synchronizeIndexProfile with reset", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "BlogPost", "Article" }, metadata.IndexedContentTypes);
        Assert.True(metadata.IndexLatest);

        // The culture was not given, so it stays.
        Assert.Equal("any", metadata.Culture);

        // The changes were validated on a copy before the stored profile was touched, then saved as the Edit action
        // of the Indexes page saves them. Nothing is indexed again by an update.
        Assert.NotSame(harness.Blog, Assert.Single(harness.IndexProfiles.Validated));
        Assert.Equal(new[] { "Update:blog" }, harness.IndexProfiles.Calls);
        Assert.Empty(harness.LuceneIndexes.Calls);
    }

    [Fact]
    public async Task SaveIndexProfile_WhenAnUpdateIsNotValid_LeavesTheStoredProfileAsItWas()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["id"] = "blog-1",
            ["name"] = "products",
            ["indexedContentTypes"] = Array.Empty<string>(),
        });

        // Assert
        Assert.Equal("Nothing was saved: There is already another index with the same name. At least one content type must be selected.", result);
        Assert.Equal("blog", harness.Blog.Name);
        Assert.Equal(new[] { "BlogPost" }, harness.Blog.GetOrCreate<ContentIndexMetadata>().IndexedContentTypes);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task SaveIndexProfile_WithAnIdAndAName_RenamesTheProfile()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["id"] = "blog-1",
            ["name"] = "blog posts",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.Equal("blog posts", harness.Blog.Name);
        Assert.Equal("blog", harness.Blog.IndexName);
        Assert.Equal(new[] { "Update:blog posts" }, harness.IndexProfiles.Calls);
    }

    [Fact]
    public async Task SaveIndexProfile_ByNameAloneForALuceneProfileWithoutAnAnalyzer_GivesItTheDefaultAnalyzer()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Blog.Alter<LuceneIndexMetadata>(metadata => metadata.AnalyzerName = null);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "blog",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("standardanalyzer", json.GetProperty("analyzerSet").GetString());
        Assert.Contains("rebuildIndexProfile", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal("standardanalyzer", harness.Blog.GetOrCreate<LuceneIndexMetadata>().AnalyzerName);
        Assert.Equal(new[] { "Update:blog" }, harness.IndexProfiles.Calls);
    }

    [Fact]
    public async Task SaveIndexProfile_ByNameAloneForAProfileWithAnAnalyzer_SavesNothing()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "blog",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: give a new name (with id), indexedContentTypes, culture or indexLatest", result, StringComparison.Ordinal);
        harness.VerifyNothingChangedOrRan();
    }

    [Theory]
    [InlineData("providerName", "Elasticsearch")]
    [InlineData("type", "Custom")]
    [InlineData("indexName", "other")]
    public async Task SaveIndexProfile_WhenAnUpdateChangesAFixedSetting_SavesNothing(string argument, string value)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveIndexProfileTool(), services, new()
        {
            ["name"] = "blog",
            [argument] = value,
            ["indexLatest"] = true,
        });

        // Assert
        Assert.StartsWith($"Nothing was saved: the {argument} of the index profile 'blog' is ", result, StringComparison.Ordinal);
        Assert.Contains("cannot be changed", result, StringComparison.Ordinal);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task SynchronizeIndexProfile_StartsTheSynchronizationAndSaysItIsNotFinished()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SynchronizeIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog", "products-1", "blog-1" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "blog-1", "products-1" }, json.GetProperty("started").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.False(json.GetProperty("reset").GetBoolean());
        Assert.Contains("starts when this reply ends", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        // Queued for when the message ends (IndexProfileSynchronization), not run as an HTTP background job.
        Assert.Empty(harness.IndexProfiles.Calls);
    }

    [Fact]
    public async Task SynchronizeIndexProfile_WithReset_ResetsTheIndexBeforeItStartsTheSynchronization()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SynchronizeIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog" },
            ["reset"] = true,
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("reset").GetBoolean());

        // The order of the Reset action of the Indexes page. The physical index is kept.
        Assert.Equal(new[] { "Reset:blog", "Update:blog" }, harness.IndexProfiles.Calls);
        Assert.Empty(harness.LuceneIndexes.Calls);
    }

    [Theory]
    [InlineData(SynchronizeIndexProfileTool.TheName, "Nothing was synchronized: ")]
    [InlineData(RebuildIndexProfileTool.TheName, "Nothing was rebuilt: ")]
    [InlineData(DeleteIndexProfileTool.TheName, "Nothing was deleted: ")]
    public async Task InvokeAsync_WhenOneOfTheIndexesIsUnknown_DoesNothingForAnyOfThem(string toolName, string expectedStart)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new()
        {
            ["indexes"] = new[] { "blog", "missing" },
        });

        // Assert
        Assert.StartsWith(expectedStart, result, StringComparison.Ordinal);
        Assert.Contains("'missing'", result, StringComparison.Ordinal);
        harness.VerifyNothingChangedOrRan();
    }

    [Theory]
    [InlineData(SynchronizeIndexProfileTool.TheName)]
    [InlineData(RebuildIndexProfileTool.TheName)]
    [InlineData(DeleteIndexProfileTool.TheName)]
    public async Task InvokeAsync_WithoutIndexes_DoesNothing(string toolName)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new()
        {
            ["indexes"] = Array.Empty<string>(),
        });

        // Assert
        Assert.Contains("indexes must be a list of IDs or names of index profiles.", result, StringComparison.Ordinal);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task RebuildIndexProfile_RecreatesTheIndexUnderTheIndexingLockAndStartsIndexing()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RebuildIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("blog-1", Assert.Single(json.GetProperty("rebuilt").EnumerateArray()).GetProperty("id").GetString());
        Assert.False(json.TryGetProperty("failed", out _));
        Assert.Contains("starts when this reply ends", json.GetProperty("note").GetString(), StringComparison.Ordinal);

        // The order of the Rebuild action of the Indexes page, under the lock the indexing of this index takes.
        Assert.Equal(new[] { "Reset:blog", "Update:blog" }, harness.IndexProfiles.Calls);
        Assert.Equal(new[] { "Rebuild:blog" }, harness.LuceneIndexes.Calls);
        Assert.Equal(new[] { "IndexingService-blog-1" }, harness.Lock.Keys);
        Assert.Equal(1, harness.Lock.Released);
    }

    [Fact]
    public async Task RebuildIndexProfile_WhenTheIndexIsBeingIndexed_LeavesItAlone()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Lock.Available = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RebuildIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var failed = Assert.Single(json.GetProperty("failed").EnumerateArray());

        Assert.Empty(json.GetProperty("rebuilt").EnumerateArray());
        Assert.Equal("blog-1", failed.GetProperty("id").GetString());
        Assert.Contains("being indexed right now", failed.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.False(json.TryGetProperty("note", out _));
        Assert.Empty(harness.IndexProfiles.Calls);
        Assert.Empty(harness.LuceneIndexes.Calls);
    }

    [Fact]
    public async Task RebuildIndexProfile_WhenTheProviderCannotRecreateTheIndex_ReportsItAndStartsNoIndexing()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.LuceneIndexes.Rebuilds = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RebuildIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog", "site" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var failed = json.GetProperty("failed").EnumerateArray().ToList();

        Assert.Empty(json.GetProperty("rebuilt").EnumerateArray());
        Assert.Equal(new[] { "blog", "site" }, failed.Select(item => item.GetProperty("name").GetString()));
        Assert.Contains("could not recreate the index", failed[0].GetProperty("error").GetString(), StringComparison.Ordinal);

        // The Azure AI Search provider has no index manager in this test, as a provider whose feature is off.
        Assert.Contains("has no index manager", failed[1].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Synchronize:blog", harness.IndexProfiles.Calls);
        Assert.Equal(1, harness.Lock.Released);
    }

    [Fact]
    public async Task DeleteIndexProfile_RemovesThePhysicalIndexAndThenTheProfile()
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog-1" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("blog", Assert.Single(json.GetProperty("deleted").EnumerateArray()).GetProperty("name").GetString());
        Assert.False(json.TryGetProperty("failed", out _));
        Assert.Equal(new[] { "Delete:blog" }, harness.LuceneIndexes.Calls);
        Assert.Equal(new[] { "Delete:blog" }, harness.IndexProfiles.Calls);
        Assert.DoesNotContain(harness.Blog, harness.IndexProfiles.Profiles);
    }

    [Fact]
    public async Task DeleteIndexProfile_WhenTheProviderCannotRemoveTheIndex_KeepsTheProfile()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.LuceneIndexes.Deletes = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var failed = Assert.Single(json.GetProperty("failed").EnumerateArray());

        Assert.Empty(json.GetProperty("deleted").EnumerateArray());
        Assert.Contains("Set force to delete the index profile anyway.", failed.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Empty(harness.IndexProfiles.Calls);
        Assert.Contains(harness.Blog, harness.IndexProfiles.Profiles);
    }

    [Fact]
    public async Task DeleteIndexProfile_WithForce_DeletesTheProfileEvenWhenTheProviderCannotRemoveTheIndex()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.LuceneIndexes.Deletes = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteIndexProfileTool(), services, new()
        {
            ["indexes"] = new[] { "blog", "site" },
            ["force"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "blog", "site" }, json.GetProperty("deleted").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(new[] { "Delete:blog" }, harness.LuceneIndexes.Calls);
        Assert.Equal(new[] { "Delete:blog", "Delete:site" }, harness.IndexProfiles.Calls);
    }

    private static AIFunction CreateTool(string toolName)
    {
        return toolName switch
        {
            SynchronizeIndexProfileTool.TheName => new SynchronizeIndexProfileTool(),
            RebuildIndexProfileTool.TheName => new RebuildIndexProfileTool(),
            DeleteIndexProfileTool.TheName => new DeleteIndexProfileTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool is not one that takes a list of indexes."),
        };
    }
}
