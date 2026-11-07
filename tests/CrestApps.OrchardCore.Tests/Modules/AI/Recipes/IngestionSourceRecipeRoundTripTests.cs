using System.Text.Json.Nodes;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.FileSources.Deployments;
using CrestApps.OrchardCore.AI.FileSources.Recipes;
using CrestApps.OrchardCore.AI.WebCrawlers.Deployments;
using CrestApps.OrchardCore.AI.WebCrawlers.Recipes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Recipes;

/// <summary>
/// File sources and web crawlers travel between sites: each is exported without its secrets, imported into an empty
/// store with the same id and settings, matched on a second run instead of duplicated, and an update that carries no
/// secret keeps the stored one.
/// </summary>
public sealed class IngestionSourceRecipeRoundTripTests
{
    private const string SourceId = "4fs00000000000000000000001";
    private const string CrawlerId = "4wc00000000000000000000001";

    [Fact]
    public async Task FileSources_ExportWithoutSecrets_AndImportIntoAnEmptyStore()
    {
        // Arrange
        var source = new InMemorySourceManager<FileSource>();
        var fileSource = new FileSource
        {
            ItemId = SourceId,
            Source = FileSystemIngestionConnector.ConnectorName,
            DisplayText = "Policies",
            AIDataSourceId = "data-source-1",
            Enabled = true,
            ReindexIntervalMinutes = 60,
            OwnerId = "owner-1",
        };

        fileSource.Properties["FtpConnectionMetadata"] = new JsonObject
        {
            ["Host"] = "ftp.example.test",
            ["Password"] = "protected-password",
            ["ValidateAnyCertificate"] = true,
        };
        source.Entries.Add(fileSource);

        var target = new InMemorySourceManager<FileSource>();
        var step = new FileSourcesStep(target, ConnectorOptions(), RecipeRoundTrip.Localizer<FileSourcesStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new FileSourcesDeploymentSource(source), new FileSourcesDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal("FileSources", exported["name"].GetValue<string>());
        Assert.DoesNotContain("protected-password", exported.ToJsonString());
        Assert.False(exported["Sources"][0].AsObject().ContainsKey("OwnerId"));

        var imported = Assert.Single(target.Entries);
        var metadata = JsonSerializerNode(imported.Properties["FtpConnectionMetadata"]);

        Assert.Equal(SourceId, imported.ItemId);
        Assert.Equal("Policies", imported.DisplayText);
        Assert.Equal("data-source-1", imported.AIDataSourceId);
        Assert.Equal(60, imported.ReindexIntervalMinutes);
        Assert.Equal("ftp.example.test", metadata["Host"].GetValue<string>());
        Assert.True(metadata["ValidateAnyCertificate"].GetValue<bool>());
        Assert.False(metadata.ContainsKey("Password"));
    }

    [Fact]
    public async Task FileSources_AnUpdateWithoutASecret_KeepsTheStoredOne()
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var stored = new FileSource { ItemId = SourceId, Source = FileSystemIngestionConnector.ConnectorName, DisplayText = "Policies" };

        stored.Properties["FtpConnectionMetadata"] = new JsonObject { ["Host"] = "old.example.test", ["Password"] = "kept" };
        target.Entries.Add(stored);

        var step = new FileSourcesStep(target, ConnectorOptions(), RecipeRoundTrip.Localizer<FileSourcesStep>());
        var recipe = JsonNode.Parse($$"""
            { "name": "FileSources", "Sources": [ { "ItemId": "{{SourceId}}", "Source": "{{FileSystemIngestionConnector.ConnectorName}}", "DisplayText": "Policies",
              "Properties": { "FtpConnectionMetadata": { "Host": "new.example.test" } } } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var metadata = JsonSerializerNode(Assert.Single(target.Entries).Properties["FtpConnectionMetadata"]);

        Assert.Equal("new.example.test", metadata["Host"].GetValue<string>());
        Assert.Equal("kept", metadata["Password"].GetValue<string>());
    }

    [Fact]
    public async Task FileSources_RefuseAConnectorThatIsNotEnabled()
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var step = new FileSourcesStep(target, ConnectorOptions(), RecipeRoundTrip.Localizer<FileSourcesStep>());
        var recipe = JsonNode.Parse("""{ "name": "FileSources", "Sources": [ { "Source": "Dropbox", "DisplayText": "Shared" } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["The file source 'Shared' uses the connector 'Dropbox', which is not enabled on this site."], errors);
        Assert.Empty(target.Entries);
    }

    [Fact]
    public async Task WebCrawlers_ExportOnlyCrawlers_AndImportIntoAnEmptyStore()
    {
        // Arrange
        var source = new InMemorySourceManager<WebCrawler>();
        var crawler = new WebCrawler
        {
            ItemId = CrawlerId,
            Source = WebCrawlerConstants.Strategies.Sitemap,
            DisplayText = "Help center",
            AIDataSourceId = "web-1",
            Enabled = true,
        };

        crawler.Properties["SitemapWebCrawlerMetadata"] = new JsonObject { ["SitemapUrl"] = "https://help.example.test/sitemap.xml", ["AccessToken"] = "secret-token" };
        source.Entries.Add(crawler);
        source.Entries.Add(new WebCrawler { ItemId = "4fl00000000000000000000001", Source = "Ftp", DisplayText = "Left-over file source" });

        var target = new InMemorySourceManager<WebCrawler>();
        var strategies = StrategyOptions();
        var step = new WebCrawlersStep(target, strategies, RecipeRoundTrip.Localizer<WebCrawlersStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new WebCrawlersDeploymentSource(source, strategies), new WebCrawlersDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal("WebCrawlers", exported["name"].GetValue<string>());
        Assert.DoesNotContain("secret-token", exported.ToJsonString());

        var imported = Assert.Single(target.Entries);

        Assert.Equal(CrawlerId, imported.ItemId);
        Assert.Equal("Help center", imported.DisplayText);
        Assert.Equal("https://help.example.test/sitemap.xml", JsonSerializerNode(imported.Properties["SitemapWebCrawlerMetadata"])["SitemapUrl"].GetValue<string>());
    }

    [Fact]
    public async Task WebCrawlers_MatchACrawlerWithoutAnIdByNameAndStrategy()
    {
        // Arrange
        var target = new InMemorySourceManager<WebCrawler>();

        target.Entries.Add(new WebCrawler { ItemId = CrawlerId, Source = WebCrawlerConstants.Strategies.Sitemap, DisplayText = "Help center" });

        var step = new WebCrawlersStep(target, StrategyOptions(), RecipeRoundTrip.Localizer<WebCrawlersStep>());
        var recipe = JsonNode.Parse($$"""
            { "name": "WebCrawlers", "Crawlers": [ { "Source": "{{WebCrawlerConstants.Strategies.Sitemap}}", "DisplayText": "help center", "ReindexIntervalMinutes": 30 } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(30, Assert.Single(target.Entries).ReindexIntervalMinutes);
    }

    [Theory]
    [InlineData("Password", true)]
    [InlineData("ProxyPassword", true)]
    [InlineData("PrivateKey", true)]
    [InlineData("OAuth2ClientSecret", true)]
    [InlineData("Host", false)]
    [InlineData("MaxTokens", false)]
    public void Secrets_AreFoundByName(string name, bool expected)
        => Assert.Equal(expected, IngestionSourceSecrets.IsSecret(name));

    private static JsonObject JsonSerializerNode(object value)
        => System.Text.Json.JsonSerializer.SerializeToNode(value, CatalogDeploymentSerializer.Options).AsObject();

    private static IOptions<IngestionConnectorOptions> ConnectorOptions()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddCoreFileSources().AddCoreFileSystemConnector();

        return Options.Create(services.BuildServiceProvider().GetRequiredService<IOptions<IngestionConnectorOptions>>().Value);
    }

    private static IOptions<WebCrawlerStrategyOptions> StrategyOptions()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddCoreWebCrawlers();

        return Options.Create(services.BuildServiceProvider().GetRequiredService<IOptions<WebCrawlerStrategyOptions>>().Value);
    }

    /// <summary>
    /// A source catalog manager kept in memory that applies JSON the way the catalog handlers do.
    /// </summary>
    private sealed class InMemorySourceManager<T> : ISourceCatalogManager<T>
        where T : IngestionSource, new()
    {
        public List<T> Entries { get; } = [];

        public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.FirstOrDefault(entry => entry.ItemId == id));

        public ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<T>>(Entries.ToList());

        public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(T model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.Remove(model));

        public ValueTask CreateAsync(T model, CancellationToken cancellationToken = default)
        {
            Entries.Add(model);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(T model, JsonNode data, CancellationToken cancellationToken = default)
        {
            CatalogDeploymentSerializer.Populate(model, data);

            return ValueTask.CompletedTask;
        }

        public ValueTask<ValidationResultDetails> ValidateAsync(T model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ValidationResultDetails());

        public ValueTask<T> NewAsync(string source, JsonNode data, CancellationToken cancellationToken = default)
        {
            var entry = new T { Source = source };

            CatalogDeploymentSerializer.Populate(entry, data);
            entry.Source = source;

            return ValueTask.FromResult(entry);
        }

        public ValueTask<IEnumerable<T>> GetAsync(string source, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<T>>(Entries.Where(entry => entry.Source == source).ToList());

        public ValueTask<IEnumerable<T>> FindBySourceAsync(string source, CancellationToken cancellationToken = default)
            => GetAsync(source, cancellationToken);
    }
}
