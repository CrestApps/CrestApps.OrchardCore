using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionMetricsIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AIChatSessionMetricsIndex>(table => table
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("ProfileId", column => column.WithLength(26))
                .Column<string>("VisitorId", column => column.WithLength(64))
                .Column<string>("UserId", column => column.WithLength(26))
                .Column<bool>("IsAuthenticated")
                .Column<DateTime>("SessionStartedUtc")
                .Column<DateTime?>("SessionEndedUtc")
                .Column<int>("MessageCount")
                .Column<double>("HandleTimeSeconds")
                .Column<bool>("IsResolved")
                .Column<int>("HourOfDay")
                .Column<int>("DayOfWeek")
                .Column<int>("TotalInputTokens", column => column.WithDefault(0))
                .Column<int>("TotalOutputTokens", column => column.WithDefault(0))
                .Column<double>("AverageResponseLatencyMs", column => column.WithDefault(0))
                .Column<bool?>("UserRating", column => column.Nullable())
                .Column<DateTime>("CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionMetricsIndex>(table => table
            .CreateIndex("IDX_AIChatSessionMetrics_DocumentId",
                "DocumentId",
                "SessionId",
                "ProfileId",
                "CreatedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionMetricsIndex>(table => table
            .CreateIndex("IDX_AIChatSessionMetrics_ProfileDate",
                "DocumentId",
                "ProfileId",
                "SessionStartedUtc",
                "SessionEndedUtc",
                "IsResolved"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionMetricsIndex>(table => table
            .CreateIndex("IDX_AIChatSessionMetrics_VisitorId",
                "DocumentId",
                "VisitorId",
                "ProfileId",
                "SessionStartedUtc"),
            collection: AIConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AIChatSessionMetricsIndex>(table => table
            .CreateIndex("IDX_AIChatSessionMetrics_TimeOfDay",
                "DocumentId",
                "ProfileId",
                "HourOfDay",
                "DayOfWeek",
                "SessionStartedUtc"),
            collection: AIConstants.CollectionName
        );

        return 1;
    }
}
