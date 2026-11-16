using YesSql.Sql;

namespace CrestApps.OrchardCore.Agreements.Core.Indexes;

/// <summary>
/// Creates the agreement index table.
/// </summary>
public static class AgreementsIndexSchema
{
    /// <summary>
    /// Creates the table and its indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = AgreementsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<AgreementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("AccountId", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AgreementIndex>(table =>
        {
            table.CreateIndex("IDX_AgreementIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AgreementIndex_Status", "Status", "Number");
            table.CreateIndex("IDX_AgreementIndex_Account", "AccountId", "Status");
        }, collection: collection);
    }
}
