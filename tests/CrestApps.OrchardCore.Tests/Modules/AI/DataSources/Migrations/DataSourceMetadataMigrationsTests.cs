using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.Migrations;

public sealed class DataSourceMetadataMigrationsTests
{
    [Fact]
    public void TryPopulateIndexConfiguration_WhenMatchingMasterProfileExists_ShouldBackfillRequiredFields()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            SourceIndexProfileName = "ContentIndex",
        };
        var sourceProfile = new IndexProfile
        {
            Name = "ContentIndex",
            ProviderName = "Elasticsearch",
            Type = IndexingConstants.ContentsIndexSource,
        };
        var masterProfiles = new[]
        {
            new IndexProfile
            {
                Name = "AI Knowledge Base Warehouse",
                ProviderName = "Elasticsearch",
                Type = DataSourceConstants.IndexingTaskType,
            },
        };
        var options = new AIDataSourceOptions();

        options.AddFieldMapping("Elasticsearch", IndexingConstants.ContentsIndexSource, mapping =>
        {
            mapping.DefaultKeyField = "ContentItemId";
            mapping.DefaultTitleField = "Content.ContentItem.DisplayText.keyword";
            mapping.DefaultContentField = "Content.ContentItem.FullText";
        });

        // Act
        var populated = InvokeTryPopulateIndexConfiguration(dataSource, sourceProfile, masterProfiles, options);

        // Assert
        Assert.True(populated);
        Assert.Equal("AI Knowledge Base Warehouse", dataSource.AIKnowledgeBaseIndexProfileName);
        Assert.Equal("ContentItemId", dataSource.KeyFieldName);
        Assert.Equal("Content.ContentItem.DisplayText.keyword", dataSource.TitleFieldName);
        Assert.Equal("Content.ContentItem.FullText", dataSource.ContentFieldName);
    }

    [Fact]
    public void TryPopulateIndexConfiguration_WhenMasterProfilesContainDifferentProvider_ShouldFallbackToFirstAvailable()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            SourceIndexProfileName = "AzureContentIndex",
        };
        var sourceProfile = new IndexProfile
        {
            Name = "AzureContentIndex",
            ProviderName = "AzureAISearch",
            Type = IndexingConstants.ContentsIndexSource,
        };
        var masterProfiles = new[]
        {
            new IndexProfile
            {
                Name = "Fallback Knowledge Base",
                ProviderName = "Elasticsearch",
                Type = DataSourceConstants.IndexingTaskType,
            },
        };
        var options = new AIDataSourceOptions();

        options.AddFieldMapping("AzureAISearch", IndexingConstants.ContentsIndexSource, mapping =>
        {
            mapping.DefaultKeyField = "ContentItemId";
            mapping.DefaultTitleField = "Content__ContentItem__DisplayText__keyword";
            mapping.DefaultContentField = "Content__ContentItem__FullText";
        });

        // Act
        var populated = InvokeTryPopulateIndexConfiguration(dataSource, sourceProfile, masterProfiles, options);

        // Assert
        Assert.True(populated);
        Assert.Equal("Fallback Knowledge Base", dataSource.AIKnowledgeBaseIndexProfileName);
        Assert.Equal("Content__ContentItem__FullText", dataSource.ContentFieldName);
    }

    private static bool InvokeTryPopulateIndexConfiguration(
        AIDataSource dataSource,
        IndexProfile sourceProfile,
        IEnumerable<IndexProfile> masterProfiles,
        AIDataSourceOptions options)
    {
        var method = typeof(CrestApps.OrchardCore.AI.DataSources.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.Migrations.DataSourceMetadataMigrations", throwOnError: true)!
            .GetMethod("TryPopulateIndexConfiguration", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [dataSource, sourceProfile, masterProfiles, options])!;
    }
}
