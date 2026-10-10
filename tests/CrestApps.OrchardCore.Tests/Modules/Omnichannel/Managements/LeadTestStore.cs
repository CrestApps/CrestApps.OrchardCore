using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A SQLite store with the content item index, the lead index and the lead import index. The lead tables come from the
/// CRM migration itself, so the lead queries under test run against the schema a tenant actually has.
/// </summary>
internal static class LeadTestStore
{
    public const string LeadContentType = "Lead";

    /// <summary>
    /// Creates the store and its index tables.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public static async Task<IStore> CreateAsync(string connectionString)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));

        store.RegisterIndexes([new ContentItemIndexProvider(), new LeadIndexProvider(), new LeadImportIndexProvider()]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("PublishedUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Owner", column => column.WithLength(255))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255)));

        await CrmMigrations.CreateLeadIndexAsync(schemaBuilder);
        await CrmMigrations.CreateLeadImportIndexAsync(schemaBuilder);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// Saves a published lead recording the given file imports.
    /// </summary>
    /// <param name="session">The session to save the lead in.</param>
    /// <param name="imports">The file imports the lead arrived in.</param>
    /// <returns>The content item identifier of the lead.</returns>
    public static async Task<string> SaveLeadAsync(ISession session, params LeadImport[] imports)
    {
        var lead = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = LeadContentType,
            DisplayText = "Lead",
            Published = true,
            Latest = true,
        };

        lead.Alter<LeadPart>(part => part.Imports = imports.ToList());

        await session.SaveAsync(lead, cancellationToken: TestContext.Current.CancellationToken);

        return lead.ContentItemId;
    }

    /// <summary>
    /// Returns a path for a database file the test deletes when it is done.
    /// </summary>
    /// <param name="suffix">A name for the test, so a leftover file says where it came from.</param>
    public static string DatabasePath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"lead-test-store-{suffix}-{Guid.NewGuid():N}.db");
}
