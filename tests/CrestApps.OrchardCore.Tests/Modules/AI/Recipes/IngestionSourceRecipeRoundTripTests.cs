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
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.DataProtection;
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
        var step = FileSources(target);

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

        stored.Properties["FtpConnectionMetadata"] = new JsonObject { ["Host"] = "ftp.example.test", ["Username"] = "deploy", ["Password"] = "kept" };
        target.Entries.Add(stored);

        var step = FileSources(target);
        var recipe = JsonNode.Parse($$"""
            { "name": "FileSources", "Sources": [ { "ItemId": "{{SourceId}}", "Source": "{{FileSystemIngestionConnector.ConnectorName}}", "DisplayText": "Policies",
              "Properties": { "FtpConnectionMetadata": { "Host": "FTP.example.test", "Username": "deploy", "ConnectTimeout": 5000 } } } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var metadata = JsonSerializerNode(Assert.Single(target.Entries).Properties["FtpConnectionMetadata"]);

        Assert.Equal(5000, metadata["ConnectTimeout"].GetValue<int>());
        Assert.Equal("kept", metadata["Password"].GetValue<string>());
    }

    /// <summary>
    /// MD5 (fix verification, round 6): a recipe that kept the stored FTP or SFTP password also kept it when it pointed
    /// the source at another server or account, which then received it with the next listing.
    /// </summary>
    [Theory]
    [InlineData("FtpConnectionMetadata", "Host", "attacker.example.test")]
    [InlineData("FtpConnectionMetadata", "Username", "root")]
    [InlineData("FtpConnectionMetadata", "Port", 2121)]
    [InlineData("SftpConnectionMetadata", "HostKeyFingerprint", "SHA256:nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8")]
    public async Task FileSources_AnUpdateThatMovesTheConnection_DropsTheKeptSecrets(string metadataName, string member, object value)
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var stored = new FileSource { ItemId = SourceId, Source = FileSystemIngestionConnector.ConnectorName, DisplayText = "Policies" };

        stored.Properties[metadataName] = new JsonObject
        {
            ["Host"] = "ftp.example.test",
            ["Port"] = 21,
            ["Username"] = "deploy",
            ["HostKeyFingerprint"] = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU",
            ["Password"] = "kept",
            ["PrivateKey"] = "kept-key",
        };
        target.Entries.Add(stored);

        var connection = JsonSerializerNode(stored.Properties[metadataName]);
        connection.Remove("Password");
        connection.Remove("PrivateKey");
        connection[member] = JsonValue.Create(value);

        var step = FileSources(target);
        var recipe = new JsonObject
        {
            ["name"] = "FileSources",
            ["Sources"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = SourceId,
                ["Source"] = FileSystemIngestionConnector.ConnectorName,
                ["DisplayText"] = "Policies",
                ["Properties"] = new JsonObject { [metadataName] = connection },
            }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var metadata = JsonSerializerNode(Assert.Single(target.Entries).Properties[metadataName]);

        Assert.Null(metadata["Password"]);
        Assert.Null(metadata["PrivateKey"]);
    }

    /// <summary>
    /// Found while fixing F9 (third fix verification): the FileSources recipe step stored an FTP or SFTP password given in
    /// clear text as it was given, while the source's editor, the API and the AIDataSource step protect it.
    /// </summary>
    [Theory]
    [InlineData("FtpConnectionMetadata", "Password")]
    [InlineData("SftpConnectionMetadata", "Password")]
    [InlineData("SftpConnectionMetadata", "PrivateKeyPassphrase")]
    public async Task FileSources_ANewSourceWithAPlainSecret_StoresItProtected(string metadataName, string member)
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var protection = new EphemeralDataProtectionProvider();
        var secretOptions = FileSourceSecretOptions();
        var step = FileSources(target, protection, secretOptions);
        var recipe = Recipe(metadataName, new JsonObject { ["Host"] = "ftp.example.test", ["Username"] = "deploy", [member] = "plain-secret" });

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var stored = JsonSerializerNode(Assert.Single(target.Entries).Properties[metadataName])[member].GetValue<string>();

        Assert.NotEqual("plain-secret", stored);
        Assert.Equal("plain-secret", protection.UnprotectSetting(secretOptions.GetPurpose(typeof(FileSource), metadataName), stored));
    }

    [Fact]
    public async Task FileSources_AnUpdateWithAPlainSecret_StoresItProtected()
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var protection = new EphemeralDataProtectionProvider();
        var secretOptions = FileSourceSecretOptions();
        var purpose = secretOptions.GetPurpose(typeof(FileSource), "FtpConnectionMetadata");
        var stored = new FileSource { ItemId = SourceId, Source = FileSystemIngestionConnector.ConnectorName, DisplayText = "Policies" };

        stored.Properties["FtpConnectionMetadata"] = new JsonObject { ["Host"] = "ftp.example.test", ["Username"] = "deploy", ["Password"] = protection.ProtectSetting(purpose, "old") };
        target.Entries.Add(stored);

        var step = FileSources(target, protection, secretOptions);
        var recipe = Recipe("FtpConnectionMetadata", new JsonObject { ["Host"] = "ftp.example.test", ["Username"] = "deploy", ["Password"] = "new-password" });

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var password = JsonSerializerNode(Assert.Single(target.Entries).Properties["FtpConnectionMetadata"])["Password"].GetValue<string>();

        Assert.Equal("new-password", protection.UnprotectSetting(purpose, password));
    }

    // A recipe is trusted, and one made on this site may carry the protected value, which is kept as it is.
    [Fact]
    public async Task FileSources_ASecretThatIsAlreadyProtected_IsKept()
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var protection = new EphemeralDataProtectionProvider();
        var secretOptions = FileSourceSecretOptions();
        var password = protection.ProtectSetting(secretOptions.GetPurpose(typeof(FileSource), "FtpConnectionMetadata"), "the-password");
        var step = FileSources(target, protection, secretOptions);
        var recipe = Recipe("FtpConnectionMetadata", new JsonObject { ["Host"] = "ftp.example.test", ["Password"] = password });

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(password, JsonSerializerNode(Assert.Single(target.Entries).Properties["FtpConnectionMetadata"])["Password"].GetValue<string>());
    }

    [Fact]
    public void RecipeRule_ComparesUserNamesOnlyForAConnectionAddressedByHost()
    {
        // An HTTP connection is addressed by its URL: a new basic user name with the same URL keeps the password, as the
        // connection editors keep it.
        var dataSource = new AIDataSource();
        dataSource.Properties["ElasticsearchSourceMetadata"] = new JsonObject { ["Url"] = "https://search.example.test/", ["Username"] = "reader", ["Password"] = "kept" };
        var before = RecipeSecretDestinations.Snapshot(dataSource);
        ((JsonObject)dataSource.Properties["ElasticsearchSourceMetadata"])["Username"] = "writer";

        Assert.False(RecipeSecretDestinations.DropKeptSecrets(dataSource, before));
    }

    [Fact]
    public async Task FileSources_RefuseAConnectorThatIsNotEnabled()
    {
        // Arrange
        var target = new InMemorySourceManager<FileSource>();
        var step = FileSources(target);
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
        var step = new WebCrawlersStep(target, strategies, new EphemeralDataProtectionProvider(), Options.Create(new IngestionSourceSecretOptions()), RecipeRoundTrip.Localizer<WebCrawlersStep>());

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

        var step = new WebCrawlersStep(target, StrategyOptions(), new EphemeralDataProtectionProvider(), Options.Create(new IngestionSourceSecretOptions()), RecipeRoundTrip.Localizer<WebCrawlersStep>());
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

    private static FileSourcesStep FileSources(ISourceCatalogManager<FileSource> target, IDataProtectionProvider protection = null, IngestionSourceSecretOptions secretOptions = null)
        => new(target, ConnectorOptions(), protection ?? new EphemeralDataProtectionProvider(), Options.Create(secretOptions ?? new IngestionSourceSecretOptions()), RecipeRoundTrip.Localizer<FileSourcesStep>());

    // The purposes the FTP and SFTP file source modules register for their connection settings.
    private static IngestionSourceSecretOptions FileSourceSecretOptions()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.FileSources.Ftp.Startup().ConfigureServices(services);
        new CrestApps.OrchardCore.AI.FileSources.Sftp.Startup().ConfigureServices(services);

        return services.BuildServiceProvider().GetRequiredService<IOptions<IngestionSourceSecretOptions>>().Value;
    }

    private static JsonObject Recipe(string metadataName, JsonObject connection)
        => new()
        {
            ["name"] = "FileSources",
            ["Sources"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = SourceId,
                ["Source"] = FileSystemIngestionConnector.ConnectorName,
                ["DisplayText"] = "Policies",
                ["Properties"] = new JsonObject { [metadataName] = connection },
            }),
        };

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
