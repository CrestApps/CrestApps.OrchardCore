using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.Sources;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class SitemapSchemaServiceTests
{
    [Fact]
    public async Task GetSourceDescriptorsAsync_IncludesEveryRegisteredSource()
    {
        var service = CreateService();

        var descriptors = await service.GetSourceDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.Name == "ContentTypesSitemapSource");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "CustomPathSitemapSource");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "SitemapIndexSource");
    }

    [Fact]
    public async Task GetSitemapSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetSitemapSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetSitemapSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetSitemapSchemaAsync_ValidatesAContentTypesSitemap()
    {
        // Arrange
        var schema = await CreateSitemapSchemaAsync();

        var json = """
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapId": "4q7ayd91z69gzv8qzta07rtpm0",
          "Name": "Main",
          "Enabled": true,
          "Path": "sitemap.xml",
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions",
              "Id": "4bee24643tz0nsgwn948venadz",
              "IndexAll": true,
              "ChangeFrequency": "Daily",
              "Priority": 5
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetSitemapSchemaAsync_ValidatesASitemapIndex()
    {
        // Arrange
        var schema = await CreateSitemapSchemaAsync();

        var json = """
        {
          "$type": "OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps",
          "Name": "Index",
          "Path": "sitemap-index.xml",
          "SitemapSources": [
            {
              "$type": "OrchardCore.Sitemaps.Models.SitemapIndexSource, OrchardCore.Sitemaps",
              "ContainedSitemapIds": []
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetSitemapSchemaAsync_AcceptsAnUnknownCustomSource()
    {
        // Arrange
        var schema = await CreateSitemapSchemaAsync();

        var json = """
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapSources": [
            {
              "$type": "My.Custom.WeatherSitemapSource, My.Custom",
              "City": "Ottawa"
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetSitemapSchemaAsync_RejectsASourceWithoutADiscriminator()
    {
        // Arrange
        var schema = await CreateSitemapSchemaAsync();

        var json = """
        {
          "$type": "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
          "SitemapSources": [
            {
              "Path": "/about"
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreateSitemapSchemaAsync()
    {
        var builder = await CreateService().GetSitemapSchemaAsync(TestContext.Current.CancellationToken);

        return builder.Build();
    }

    private static bool Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);

        var result = schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
        });

        return result.IsValid;
    }

    private static SitemapSchemaService CreateService()
    {
        var sources = new ISitemapSourceSchemaDefinition[]
        {
            new ContentTypesSitemapSourceSchema(),
            new CustomPathSitemapSourceSchema(),
            new SitemapIndexSourceSchema(),
        };

        return new SitemapSchemaService(sources);
    }
}
