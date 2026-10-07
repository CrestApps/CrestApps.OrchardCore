using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="PredictivePacingStateIndex"/> and keeps one pacing record per campaign queue
/// through a unique constraint, so two nodes pacing a queue for the first time cannot both create one.
/// </summary>
internal sealed class PredictivePacingStateIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingStateIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public PredictivePacingStateIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the pacing-record index table and its per-queue uniqueness constraint.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PredictivePacingStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.NotNull().WithLength(ContactCenterStorage.QueueIdLength)),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PredictivePacingStateIndex>(table => table
            .CreateIndex(
                "IDX_PredictivePacingStateIndex_Queue",
                "QueueId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(PredictivePacingStateIndex),
            "UQ_PredictivePacingStateIndex_Queue",
            "QueueId");

        return 1;
    }
}
