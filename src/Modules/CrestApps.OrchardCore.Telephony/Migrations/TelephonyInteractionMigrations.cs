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
            .Column<bool>("IsExtension")
            .Column<CallOutcome>("Outcome")
            .Column<DateTime>("StartedUtc")
            .Column<bool>("IsVoicemail")
            .Column<DateTime>("VoicemailReadUtc", column => column.Nullable())
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

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_Voicemail",
                "UserId",
                "IsVoicemail",
                "VoicemailReadUtc",
                "DocumentId")
        );

        return 3;
    }

    // Adds the voicemail columns to an existing telephony interaction index so a call sent to voicemail can be
    // surfaced (and its unread state tracked) in the soft phone's history.
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .AddColumn<bool>("IsVoicemail", column => column.WithDefault(false))
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .AddColumn<DateTime>("VoicemailReadUtc", column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_Voicemail",
                "UserId",
                "IsVoicemail",
                "VoicemailReadUtc",
                "DocumentId")
        );

        return 2;
    }

    // Adds the extension-call marker column to an existing telephony interaction index so the Recent tab can
    // redial an internal extension entry in extension mode.
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .AddColumn<bool>("IsExtension", column => column.WithDefault(false))
        );

        return 3;
    }
}
