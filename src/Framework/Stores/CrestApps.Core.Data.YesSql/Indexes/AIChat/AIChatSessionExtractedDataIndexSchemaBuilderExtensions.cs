using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public static class AIChatSessionExtractedDataIndexSchemaBuilderExtensions
{
    public static async Task CreateAIChatSessionExtractedDataIndexSchemaAsync(this ISchemaBuilder schemaBuilder, string collectionName = null)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AIChatSessionExtractedDataIndex>(table => table
            .Column<string>(nameof(AIChatSessionExtractedDataIndex.SessionId), column => column.WithLength(26))
            .Column<string>(nameof(AIChatSessionExtractedDataIndex.ProfileId), column => column.WithLength(26))
            .Column<DateTime>(nameof(AIChatSessionExtractedDataIndex.SessionStartedUtc))
            .Column<DateTime?>(nameof(AIChatSessionExtractedDataIndex.SessionEndedUtc))
            .Column<int>(nameof(AIChatSessionExtractedDataIndex.FieldCount))
            .Column<string>(nameof(AIChatSessionExtractedDataIndex.FieldNames), column => column.WithLength(4000))
            .Column<string>(nameof(AIChatSessionExtractedDataIndex.ValuesText), column => column.WithLength(4000))
            .Column<DateTime>(nameof(AIChatSessionExtractedDataIndex.UpdatedUtc)),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionExtractedDataIndex>(table => table
            .CreateIndex(
                "IDX_AIChatSessionExtractedData_ProfileDate",
                "DocumentId",
                nameof(AIChatSessionExtractedDataIndex.ProfileId),
                nameof(AIChatSessionExtractedDataIndex.SessionStartedUtc),
                nameof(AIChatSessionExtractedDataIndex.UpdatedUtc)),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<AIChatSessionExtractedDataIndex>(table => table
            .CreateIndex(
                "IDX_AIChatSessionExtractedData_SessionId",
                "DocumentId",
                nameof(AIChatSessionExtractedDataIndex.SessionId),
                nameof(AIChatSessionExtractedDataIndex.ProfileId)),
            collection: collectionName);
    }
}
