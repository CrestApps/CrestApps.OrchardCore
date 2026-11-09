using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

internal sealed class ContactAddressOptOutIndexMigrations : OmnichannelIndexMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactAddressOptOutIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public ContactAddressOptOutIndexMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<ContactAddressOptOutIndexMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
    }

    /// <summary>
    /// Creates the index of opt-outs recorded against an address.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await CreateIndexTableAsync(SchemaBuilder);

        return 1;
    }

    /// <summary>
    /// Creates the index table and its lookups. Shared with the tests that build the schema without running migrations.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    internal static async Task CreateIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ContactAddressOptOutIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("Address", column => column.WithLength(255)),
        collection: OmnichannelConstants.CollectionName
        );

        await schemaBuilder.AlterIndexTableAsync<ContactAddressOptOutIndex>(table => table
            .CreateIndex("IDX_ContactAddressOptOutIndex_Address",
                "Address",
                "Channel"
            ),
        collection: OmnichannelConstants.CollectionName
        );

        await schemaBuilder.AlterIndexTableAsync<ContactAddressOptOutIndex>(table => table
            .CreateIndex(
                "IDX_ContactAddressOptOutIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: OmnichannelConstants.CollectionName);
    }
}
