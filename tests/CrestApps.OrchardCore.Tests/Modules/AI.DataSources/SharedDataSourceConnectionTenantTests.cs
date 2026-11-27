using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 3 (Medium, AI): the globally configured Elasticsearch connection and PostgreSQL connection string carry the
/// host's credentials, and any tenant could read any index or table through them, other tenants' included. As for
/// Azure AI Search and SQL Server signing in as the host, they now work on the default tenant, and on another tenant
/// only for the indexes and tables the host approved for it, when a data source is saved and before every sync.
/// </summary>
public sealed class SharedDataSourceConnectionTenantTests
{
    private const string Tenant = "Customer1";

    [Fact]
    public async Task Elasticsearch_OnAnotherTenant_AnIndexTheHostDidNotApprove_IsRefused()
    {
        // Arrange
        var handler = CreateElasticsearch(Tenant, approved: ["customer1-docs"]);
        var dataSource = ElasticsearchSource("other-tenant-docs");
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ElasticsearchSharedConnectionAccess.NotApprovedError);
        Assert.Equal(ElasticsearchSharedConnectionAccess.NotApprovedError, exception.Message);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("customer1-docs,other-tenant-docs")]
    public async Task Elasticsearch_OnAnotherTenant_APatternOrListOfIndexes_IsRefused(string indexName)
    {
        // Arrange
        var handler = CreateElasticsearch(Tenant, approved: ["customer1-docs"]);
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(ElasticsearchSource(indexName), result, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(Tenant, "customer1-docs")]
    [InlineData(ShellSettings.DefaultShellName, "any-index")]
    public async Task Elasticsearch_AnApprovedIndex_OrAnyIndexOnTheDefaultTenant_IsAccepted(string tenant, string indexName)
    {
        // Arrange
        var handler = CreateElasticsearch(tenant, approved: ["customer1-docs"]);
        var dataSource = ElasticsearchSource(indexName);
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var (client, _) = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task PostgreSQL_OnAnotherTenant_ATableTheHostDidNotApprove_IsRefused()
    {
        // Arrange
        var handler = CreatePostgreSQL(Tenant, approved: ["customer1.articles"]);
        var dataSource = PostgreSQLSource("other_tenant.articles");
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in handler.ReadAsync(dataSource, TestContext.Current.CancellationToken))
            {
            }
        });

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == PostgreSQLConnectionGuard.NotApprovedError);
        Assert.Equal(PostgreSQLConnectionGuard.NotApprovedError, exception.Message);
    }

    [Theory]
    [InlineData(Tenant, "customer1.articles", true)]
    [InlineData(Tenant, " customer1 . articles ", true)]
    [InlineData(Tenant, "customer1.Articles", false)]
    [InlineData(ShellSettings.DefaultShellName, "any.table", true)]
    public async Task PostgreSQL_OnlyAnApprovedTable_OrAnyTableOnTheDefaultTenant_IsAccepted(string tenant, string tableName, bool accepted)
    {
        // Arrange
        var handler = CreatePostgreSQL(tenant, approved: ["customer1.articles"]);
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(PostgreSQLSource(tableName), result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(accepted, result.Succeeded);
    }

    private static ElasticsearchAIDataSourceSourceHandler CreateElasticsearch(string tenant, string[] approved)
        => new(
            new EphemeralDataProtectionProvider(),
            Options.Create(new ElasticsearchDataSourceOptions { Url = "https://search.example.com:9243" }),
            NullLogger<ElasticsearchAIDataSourceSourceHandler>.Instance,
            sharedConnectionAccess: new ElasticsearchSharedConnectionAccess(
                new ShellSettings { Name = tenant },
                Options.Create(new ElasticsearchSharedConnectionOptions { SharedConnectionIndexes = [.. approved] })));

    private static PostgreSQLAIDataSourceSourceHandler CreatePostgreSQL(string tenant, string[] approved)
        => new(
            new EphemeralDataProtectionProvider(),
            Options.Create(new PostgreSQLDataSourceOptions { ConnectionString = "Host=db.example.com;Database=shared;Username=reader;Password=secret" }),
            NullLogger<PostgreSQLAIDataSourceSourceHandler>.Instance,
            new PostgreSQLConnectionGuard(
                new ShellSettings { Name = tenant },
                Options.Create(new PostgreSQLSharedConnectionOptions { SharedConnectionTables = [.. approved] })));

    private static AIDataSource ElasticsearchSource(string indexName)
    {
        var dataSource = new AIDataSource { ItemId = "ds-es", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Docs" };
        dataSource.Put(new ElasticsearchSourceMetadata { IndexName = indexName });

        return dataSource;
    }

    private static AIDataSource PostgreSQLSource(string tableName)
    {
        var dataSource = new AIDataSource { ItemId = "ds-pg", Source = AIDataSourceSourceTypes.PostgreSQL, DisplayText = "Articles" };
        dataSource.Put(new PostgreSQLSourceMetadata { TableName = tableName });

        return dataSource;
    }
}
