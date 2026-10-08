using CrestApps.Core.Data.YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Creates the table behind <see cref="AIVoiceSessionSummaryIndex"/>.
/// </summary>
public static class AIVoiceSessionSummaryIndexSchema
{
    /// <summary>
    /// The length of the name columns: deployment, model, connection, profile and campaign names.
    /// </summary>
    public const int NameLength = 255;

    private const int IdLength = 26;

    /// <summary>
    /// Creates the index table and the indexes the report and the write-once check read through.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the running migration.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIVoiceSessionSummaryIndex>(table => table
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.ItemId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.ActivityId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.AISessionId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.AIProfileId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.AIProfileName), column => column.WithLength(NameLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.CampaignId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.CampaignName), column => column.WithLength(NameLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.Channel), column => column.WithLength(64))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.ChannelEndpointId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.Engine), column => column.WithLength(32))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.Outcome), column => column.WithLength(32))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.DeploymentName), column => column.WithLength(NameLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.ModelName), column => column.WithLength(NameLength))
            .Column<string>(nameof(AIVoiceSessionSummaryIndex.ConnectionName), column => column.WithLength(NameLength))
            .Column<DateTime?>(nameof(AIVoiceSessionSummaryIndex.StartedUtc))
            .Column<DateTime?>(nameof(AIVoiceSessionSummaryIndex.EndedUtc))
            .Column<DateTime>(nameof(AIVoiceSessionSummaryIndex.CreatedUtc), column => column.NotNull())
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.SessionDurationMs))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.AssistantSpeakingMs))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.CallerSpeakingMs))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.MutualSilenceMs))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.TimeToFirstAssistantAudioMs))
            .Column<int>(nameof(AIVoiceSessionSummaryIndex.AssistantTurns))
            .Column<int>(nameof(AIVoiceSessionSummaryIndex.CallerTurns))
            .Column<int?>(nameof(AIVoiceSessionSummaryIndex.BargeIns))
            .Column<int>(nameof(AIVoiceSessionSummaryIndex.IdlePrompts))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.EchoHeldMs))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.InputAudioTokens))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.OutputAudioTokens))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.InputTextTokens))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.OutputTextTokens))
            .Column<long?>(nameof(AIVoiceSessionSummaryIndex.CachedInputTokens)),
            collection: options.AICollectionName);

        // The report reads a date range, so the range is what is indexed.
        await schemaBuilder.AlterIndexTableAsync<AIVoiceSessionSummaryIndex>(
            table => table.CreateIndex(
                "IDX_AIVoiceSessionSummary_CreatedUtc",
                nameof(AIVoiceSessionSummaryIndex.CreatedUtc),
                "DocumentId"),
            collection: options.AICollectionName);

        // A call is summarized once; the writer looks the activity up before it writes.
        await schemaBuilder.AlterIndexTableAsync<AIVoiceSessionSummaryIndex>(
            table => table.CreateIndex(
                "IDX_AIVoiceSessionSummary_ActivityId",
                nameof(AIVoiceSessionSummaryIndex.ActivityId),
                "DocumentId"),
            collection: options.AICollectionName);
    }
}
