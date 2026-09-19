using CrestApps.Core.Data.YesSql.Telephony.Indexes;
using CrestApps.Core.Data.YesSql.Migrations;
using YesSql.Sql;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Data.YesSql.Telephony.Migrations;

/// <summary>
/// Creates the schema used to store telephony interactions for history and reporting.
/// </summary>
public sealed class TelephonyInteractionSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The recorded name is the Orchard migration class this step was lifted out of, so a database
    /// migrated under either host agrees on which version is already applied.
    /// </remarks>
    public string Name => "TelephonyInteractionMigrations";

    /// <inheritdoc/>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<TelephonyInteractionIndex>(table => table
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

        await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_Search",
                "UserId",
                "StartedUtc",
                "ProviderName",
                "Direction",
                "Outcome",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_CallId",
                "UserId",
                "CallId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_InteractionId",
                "InteractionId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
            .CreateIndex("IDX_TelephonyInteractionIndex_Voicemail",
                "UserId",
                "IsVoicemail",
                "VoicemailReadUtc",
                "DocumentId")
        );

        return 3;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                // Adds the voicemail columns to an existing telephony interaction index so a call sent to voicemail can be
                // surfaced (and its unread state tracked) in the soft phone's history.
                await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
                    .AddColumn<bool>("IsVoicemail", column => column.WithDefault(false))
                );

                await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
                    .AddColumn<DateTime>("VoicemailReadUtc", column => column.Nullable())
                );

                await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
                    .CreateIndex("IDX_TelephonyInteractionIndex_Voicemail",
                        "UserId",
                        "IsVoicemail",
                        "VoicemailReadUtc",
                        "DocumentId")
                );

                return 2;

            case 2:
                // Adds the extension-call marker column to an existing telephony interaction index so the Recent tab can
                // redial an internal extension entry in extension mode.
                await builder.AlterIndexTableAsync<TelephonyInteractionIndex>(table => table
                    .AddColumn<bool>("IsExtension", column => column.WithDefault(false))
                );

                return 3;

            default:
                return version;
        }
    }
}
