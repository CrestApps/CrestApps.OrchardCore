using CrestApps.Core.Data.YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Creates the table behind <see cref="AIConversationEvaluationIndex"/>.
/// </summary>
public static class AIConversationEvaluationIndexSchema
{
    /// <summary>
    /// The length of the label column.
    /// </summary>
    public const int LabelLength = 255;

    private const int IdLength = 26;

    /// <summary>
    /// Creates the index table and the indexes reports and the session lookup read through.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the running migration.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIConversationEvaluationIndex>(table => table
            .Column<string>(nameof(AIConversationEvaluationIndex.ItemId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIConversationEvaluationIndex.SessionId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIConversationEvaluationIndex.ProfileId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIConversationEvaluationIndex.Label), column => column.WithLength(LabelLength))
            .Column<int>(nameof(AIConversationEvaluationIndex.Score))
            .Column<bool>(nameof(AIConversationEvaluationIndex.Passed))
            .Column<bool>(nameof(AIConversationEvaluationIndex.Resolved))
            .Column<int>(nameof(AIConversationEvaluationIndex.IssueCount))
            .Column<string>(nameof(AIConversationEvaluationIndex.IssueKinds), column => column.WithLength(LabelLength))
            .Column<DateTime>(nameof(AIConversationEvaluationIndex.EvaluatedUtc), column => column.NotNull()),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIConversationEvaluationIndex>(
            table => table.CreateIndex(
                "IDX_AIConversationEvaluation_EvaluatedUtc",
                nameof(AIConversationEvaluationIndex.EvaluatedUtc),
                "DocumentId"),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIConversationEvaluationIndex>(
            table => table.CreateIndex(
                "IDX_AIConversationEvaluation_SessionId",
                nameof(AIConversationEvaluationIndex.SessionId),
                "DocumentId"),
            collection: options.AICollectionName);
    }
}
