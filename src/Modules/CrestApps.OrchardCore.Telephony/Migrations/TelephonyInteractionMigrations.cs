using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema used to store telephony interactions for history and reporting.
/// </summary>
public sealed class TelephonyInteractionMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TelephonyInteractionIndex>(table => table
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("CallId", column => column.WithLength(128))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("UserName", column => column.WithLength(255))
            .Column<CallDirection>("Direction")
            .Column<CallOutcome>("Outcome")
            .Column<DateTime>("StartedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_Search",
                "UserId",
                "StartedUtc",
                "ProviderName",
                "Direction",
                "Outcome",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_CallId",
                "UserId",
                "CallId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_InteractionId",
                "InteractionId",
                "DocumentId")
        );

        return 1;
    }
}
