using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallSessionLegIndex"/>.
/// </summary>
/// <remarks>
/// Sessions stored before the index existed get their rows the next time they are saved; a call already over is not
/// saved again, so it can still be found only by its first leg.
/// </remarks>
internal sealed class CallSessionLegIndexMigrations : DataMigration
{
    // A provider leg id is supplied verbatim by the provider, the same as the call id it is sized like.
    private const int ProviderLegIdLength = 256;

    /// <summary>
    /// Creates the call session leg index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallSessionLegIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderLegId", column => column.WithLength(ProviderLegIdLength))
            .Column<CallPartyRole>("Role")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<DateTime>("StartedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionLegIndex>(table => table
            .CreateIndex(
                "IDX_CallSessionLegIndex_ProviderLegId",
                "ProviderLegId",
                "StartedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
