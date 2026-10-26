using System.Security.Claims;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The AI usage data sets of the AI chat data source read real completion usage records and voice session summaries
/// from a SQLite store built by the AI module's own migrations, so the index queries, the fields they expose, the
/// push-down of dates, join keys and names, the grouping in the database, and the permission they require are all
/// exercised end to end.
/// </summary>
public sealed class AIUsageReportDataSetsTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Analyst");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IOptions<YesSqlStoreOptions> _storeOptions = Options.Create(new YesSqlStoreOptions());
    private static readonly string _longProfileName = new('p', AIVoiceSessionSummaryIndexSchema.NameLength + 20);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ai-usage-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var collection = _storeOptions.Value.AICollectionName;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new AICompletionUsageIndexProvider(_storeOptions), new AIVoiceSessionSummaryIndexProvider(_storeOptions)], collection);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(collection, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new AICompletionUsageIndexMigrations(_storeOptions) { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new AIVoiceSessionSummaryIndexMigrations(_storeOptions) { SchemaBuilder = schemaBuilder }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // A chat completion, an embedding without a model name, a chat completion recorded before kinds were, and
            // a speech-to-text request, one a day.
            await session.SaveAsync(new AICompletionUsageRecord
            {
                OperationType = AIUsageOperationTypes.Chat,
                ContextType = "AIChatSession",
                Purpose = "Answer",
                SessionId = "session-0",
                ProfileId = "profile-support",
                InteractionId = "interaction-0",
                UserId = "user-1",
                UserName = "ada",
                VisitorId = "visitor-secret",
                ClientId = "fingerprint-secret",
                IsAuthenticated = true,
                ClientName = "OpenAI",
                ConnectionName = "default",
                DeploymentName = "gpt-4o-deployment",
                ModelName = "gpt-4o",
                ResponseId = "response-secret",
                IsStreaming = true,
                InputTokenCount = 100,
                OutputTokenCount = 50,
                TotalTokenCount = 150,
                CachedInputTokenCount = 20,
                ReasoningTokenCount = 5,
                ResponseLatencyMs = 812.5,
                CreatedUtc = _start,
            }, false, _storeOptions.Value.AICollectionName, cancellationToken);

            await session.SaveAsync(new AICompletionUsageRecord
            {
                OperationType = AIUsageOperationTypes.Embedding,
                ContextType = "Indexing",
                ClientName = "OpenAI",
                DeploymentName = "embedding-deployment",
                ModelName = string.Empty,
                InputTokenCount = 400,
                TotalTokenCount = 400,
                CreatedUtc = _start.AddDays(1),
            }, false, _storeOptions.Value.AICollectionName, cancellationToken);

            await session.SaveAsync(new AICompletionUsageRecord
            {
                ContextType = "AIChatSession",
                SessionId = "session-2",
                ProfileId = "profile-sales",
                UserId = "user-2",
                ClientName = "OpenAI",
                DeploymentName = "gpt-4o-deployment",
                ModelName = "gpt-4o",
                InputTokenCount = 30,
                OutputTokenCount = 10,
                TotalTokenCount = 40,
                CreatedUtc = _start.AddDays(2),
            }, false, _storeOptions.Value.AICollectionName, cancellationToken);

            await session.SaveAsync(new AICompletionUsageRecord
            {
                OperationType = AIUsageOperationTypes.SpeechToText,
                ContextType = "Voice",
                ClientName = "AzureSpeech",
                DeploymentName = "whisper-deployment",
                ModelName = "whisper",
                AudioDurationMs = 90_000,
                CreatedUtc = _start.AddDays(3),
            }, false, _storeOptions.Value.AICollectionName, cancellationToken);

            // Three voice calls, one a day: a realtime call handed to an agent, a turn-based call that could not
            // measure the caller, and a call held by a profile whose name is longer than the index keeps.
            for (var day = 0; day < 3; day++)
            {
                await session.SaveAsync(new AIVoiceSessionSummary
                {
                    ItemId = $"summary-{day}",
                    ActivityId = $"activity-{day}",
                    AISessionId = $"session-{day}",
                    AIProfileId = day == 2 ? "profile-long" : "profile-voice",
                    AIProfileName = day == 2 ? _longProfileName : "Voice agent",
                    CampaignId = day == 0 ? "campaign-1" : null,
                    CampaignName = day == 0 ? "Renewals" : null,
                    Channel = "Phone",
                    ChannelEndpointId = "endpoint-1",
                    ProviderName = "Twilio",
                    ProviderCallId = "call-secret",
                    Engine = day == 1 ? AIVoiceSessionEngine.TurnBased : AIVoiceSessionEngine.Realtime,
                    Outcome = day == 0 ? AIVoiceSessionOutcome.HandedToAgent : AIVoiceSessionOutcome.CompletedByAI,
                    DeploymentName = "realtime-deployment",
                    ModelName = "gpt-realtime",
                    ConnectionName = "default",
                    StartedUtc = _start.AddDays(day),
                    EndedUtc = _start.AddDays(day).AddMinutes(2),
                    CreatedUtc = _start.AddDays(day).AddMinutes(2),
                    SessionDurationMs = 120_000,
                    AssistantSpeakingMs = 60_000,
                    CallerSpeakingMs = day == 1 ? null : 30_000,
                    MutualSilenceMs = day == 1 ? null : 30_000,
                    TimeToFirstAssistantAudioMs = 900,
                    AssistantTurns = 4,
                    CallerTurns = 3,
                    BargeIns = day == 1 ? null : 1,
                    IdlePrompts = 0,
                    InputAudioTokens = day == 1 ? null : 1_000,
                    OutputAudioTokens = day == 1 ? null : 2_000,
                }, false, _storeOptions.Value.AICollectionName, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewChatAnalytics_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canViewAnalytics: false);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var completionSchema = await source.GetSchemaAsync(AIChatReportDataSource.CompletionUsageDataSet, context, TestContext.Current.CancellationToken);
        var voiceSchema = await source.GetSchemaAsync(AIChatReportDataSource.VoiceUsageDataSet, context, TestContext.Current.CancellationToken);
        var completions = await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.CompletionUsageDataSet), TestContext.Current.CancellationToken);
        var calls = await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.VoiceUsageDataSet), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(completionSchema);
        Assert.Null(voiceSchema);
        Assert.Empty(completions.Rows);
        Assert.Empty(calls.Rows);
    }

    [Fact]
    public async Task WithoutAPrincipal_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.CompletionUsageDataSet, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.CompletionUsageDataSet, user: null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_CompletionUsage_DeclaresKeysAndReferences_AndNeverExposesClientIdentifiers()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.CompletionUsageDataSet, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AIChatReportDataSource.CompletionUsageDataSet, AIChatReportDataSource.VoiceUsageDataSet], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(AICompletionUsageReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);

        var sessionReference = Assert.Single(schema.FindField(AIChatReportDataSource.SessionIdField).References);
        Assert.Equal(AIChatReportDataSource.SourceName, sessionReference.Source);
        Assert.Equal(AIChatReportDataSource.ChatSessionsDataSet, sessionReference.DataSet);
        Assert.Equal(AIChatReportDataSource.SessionIdField, sessionReference.Field);

        var userReference = Assert.Single(schema.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, userReference.Source);
        Assert.Equal(ReportsConstants.UserIdField, userReference.Field);

        Assert.All(new[] { AIChatReportDataSource.SessionIdField, "ProfileId", "InteractionId", "UserId" }, name => Assert.True(schema.FindField(name).IsKeyFilterable));
        Assert.Equal(DataFieldType.Integer, schema.FindField("InputTokenCount").Type);
        Assert.Equal(DataFieldType.Decimal, schema.FindField("AudioMinutes").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "ClientId" or "VisitorId" or "ResponseId" or "Properties");
    }

    [Fact]
    public async Task Query_CompletionUsage_ReturnsEveryMeteredRequest_WithTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.CompletionUsageDataSet), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["SpeechToText", "Chat", "Embedding", "Chat"], rows.Select(row => row["OperationType"]));
        Assert.Equal([_start.AddDays(3), _start.AddDays(2), _start.AddDays(1), _start], rows.Select(row => row[AICompletionUsageReportDataSet.CreatedUtcField]));
        Assert.All(rows, row => Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(row[AICompletionUsageReportDataSet.CreatedUtcField]).Kind));

        var speech = rows[0];
        Assert.Equal(90_000L, speech["AudioDurationMs"]);
        Assert.Equal(1.5m, speech["AudioMinutes"]);
        Assert.Equal("whisper", speech[AICompletionUsageReportDataSet.ModelField]);

        var legacy = rows[1];
        Assert.Equal("Sales", legacy["ProfileName"]);
        Assert.Equal("session-2", legacy[AIChatReportDataSource.SessionIdField]);

        var embedding = rows[2];
        Assert.True(string.IsNullOrEmpty(embedding["ModelName"] as string));
        Assert.Equal("embedding-deployment", embedding[AICompletionUsageReportDataSet.ModelField]);
        Assert.Equal(400L, embedding["InputTokenCount"]);
        Assert.Null(embedding["ProfileName"]);

        var chat = rows[3];
        Assert.Equal("AIChatSession", chat["ContextType"]);
        Assert.Equal("Answer", chat["Purpose"]);
        Assert.Equal("OpenAI", chat["ClientName"]);
        Assert.Equal("default", chat["ConnectionName"]);
        Assert.Equal("gpt-4o-deployment", chat["DeploymentName"]);
        Assert.Equal("gpt-4o", chat[AICompletionUsageReportDataSet.ModelField]);
        Assert.Equal("profile-support", chat["ProfileId"]);
        Assert.Equal("Support", chat["ProfileName"]);
        Assert.Equal("interaction-0", chat["InteractionId"]);
        Assert.Equal("user-1", chat["UserId"]);
        Assert.Equal("ada", chat["UserName"]);
        Assert.Equal(true, chat["IsAuthenticated"]);
        Assert.Equal(true, chat["IsStreaming"]);
        Assert.Equal(100L, chat["InputTokenCount"]);
        Assert.Equal(50L, chat["OutputTokenCount"]);
        Assert.Equal(150L, chat["TotalTokenCount"]);
        Assert.Equal(20L, chat["CachedInputTokenCount"]);
        Assert.Equal(5L, chat["ReasoningTokenCount"]);
        Assert.Equal(812.5m, chat["ResponseLatencyMs"]);
        Assert.DoesNotContain(chat.Values, value => value is string text && text.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_CompletionUsage_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(AIChatReportDataSource.CompletionUsageDataSet, AICompletionUsageReportDataSet.CreatedUtcField);
        dates.Conditions.Add(Condition(AICompletionUsageReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(AIChatReportDataSource.CompletionUsageDataSet, AICompletionUsageReportDataSet.CreatedUtcField);
        keys.Conditions.Add(new DataCondition { Field = AIChatReportDataSource.SessionIdField, Operator = DataFilterOperator.In, Values = ["session-0", "session-2"], IsJoinKey = true });
        var model = Query(AIChatReportDataSource.CompletionUsageDataSet, AICompletionUsageReportDataSet.CreatedUtcField);
        model.Conditions.Add(Condition("ModelName", DataFilterOperator.Equals, "GPT-4O"));
        var none = Query(AIChatReportDataSource.CompletionUsageDataSet, AICompletionUsageReportDataSet.CreatedUtcField);
        none.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var modelRows = Rows(await ReportDesignerTestServices.ReadAsync(source, model, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The bounds are inclusive, so the rows on them stay; the report applies the strict comparison itself.
        Assert.Equal([_start.AddDays(2), _start.AddDays(1)], dateRows.Select(row => row[AICompletionUsageReportDataSet.CreatedUtcField]));
        Assert.Equal([_start.AddDays(2), _start], keyRows.Select(row => row[AICompletionUsageReportDataSet.CreatedUtcField]));
        Assert.Equal([_start.AddDays(2), _start], modelRows.Select(row => row[AICompletionUsageReportDataSet.CreatedUtcField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CompletionUsage_SumsTokensByDeploymentInTheDatabase_AndDeclinesTheProfileName()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byDeployment = new DataAggregateQuery
        {
            DataSet = AIChatReportDataSource.CompletionUsageDataSet,
            Groups = [new DataAggregateGroup { Field = "DeploymentName" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Count },
                new DataAggregateMeasure { Field = "InputTokenCount", Function = DataAggregate.Sum },
            ],
            Context = new DataSourceContext { User = _user },
        };
        var byProfileName = new DataAggregateQuery
        {
            DataSet = AIChatReportDataSource.CompletionUsageDataSet,
            Groups = [new DataAggregateGroup { Field = "ProfileName" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(byDeployment, TestContext.Current.CancellationToken);
        var declined = await ((IDataAggregateSource)source).AggregateAsync(byProfileName, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (Count: (long)row[1], Input: Convert.ToInt64(row[2])));
        Assert.Equal((2L, 130L), counts["gpt-4o-deployment"]);
        Assert.Equal((1L, 400L), counts["embedding-deployment"]);
        Assert.Equal((1L, 0L), counts["whisper-deployment"]);
        Assert.Null(declined);
    }

    [Fact]
    public async Task Query_VoiceUsage_ReturnsTypedValues_NewestFirst_AndReferencesChatSessions()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var schema = await source.GetSchemaAsync(AIChatReportDataSource.VoiceUsageDataSet, context, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.VoiceUsageDataSet), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(AIVoiceUsageReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        var sessionReference = Assert.Single(schema.FindField("AISessionId").References);
        Assert.Equal(AIChatReportDataSource.SourceName, sessionReference.Source);
        Assert.Equal(AIChatReportDataSource.ChatSessionsDataSet, sessionReference.DataSet);
        Assert.Equal(AIChatReportDataSource.SessionIdField, sessionReference.Field);
        Assert.True(schema.FindField("ActivityId").IsKeyFilterable);

        var activityReference = Assert.Single(schema.FindField("ActivityId").References);
        Assert.Equal("Omnichannel", activityReference.Source);
        Assert.Equal("Activities", activityReference.DataSet);
        Assert.Equal("ItemId", activityReference.Field);
        Assert.Equal("Campaigns", Assert.Single(schema.FindField("CampaignId").References).DataSet);
        Assert.DoesNotContain(schema.Fields, field => field.Name == "ProviderCallId");

        Assert.Equal(["summary-2", "summary-1", "summary-0"], rows.Select(row => row["ItemId"]));

        var handedOver = rows[2];
        Assert.Equal("activity-0", handedOver["ActivityId"]);
        Assert.Equal("session-0", handedOver["AISessionId"]);
        Assert.Equal("Voice agent", handedOver["AIProfileName"]);
        Assert.Equal("Renewals", handedOver["CampaignName"]);
        Assert.Equal("Realtime", handedOver["Engine"]);
        Assert.Equal("HandedToAgent", handedOver["Outcome"]);
        Assert.Equal("gpt-realtime", handedOver["ModelName"]);
        Assert.Equal("Twilio", handedOver["ProviderName"]);
        Assert.Equal(120_000L, handedOver["SessionDurationMs"]);
        Assert.Equal(120m, handedOver["SessionSeconds"]);
        Assert.Equal(2m, handedOver["SessionMinutes"]);
        Assert.Equal(1m, handedOver["AIMinutes"]);
        Assert.Equal(0.5m, handedOver["CallerMinutes"]);
        Assert.Equal(1L, handedOver["BargeIns"]);
        Assert.Equal(2_000L, handedOver["OutputAudioTokens"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(handedOver["StartedUtc"]).Kind);

        var turnBased = rows[1];
        Assert.Equal("TurnBased", turnBased["Engine"]);
        Assert.Null(turnBased["CallerSpeakingMs"]);
        Assert.Null(turnBased["CallerMinutes"]);
        Assert.Null(turnBased["BargeIns"]);
        Assert.Null(turnBased["CampaignId"]);

        var longName = Assert.IsType<string>(rows[0]["AIProfileName"]);
        Assert.Equal(AIVoiceSessionSummaryIndexSchema.NameLength, longName.Length);
        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && text.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_VoiceUsage_DatesAndJoinKeys_NeverDropAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(AIChatReportDataSource.VoiceUsageDataSet, "ItemId");
        dates.Conditions.Add(Condition(AIVoiceUsageReportDataSet.CreatedUtcField, DataFilterOperator.GreaterThanOrEqual, _start.AddDays(1).AddMinutes(2)));
        var keys = Query(AIChatReportDataSource.VoiceUsageDataSet, "ItemId");
        keys.Conditions.Add(new DataCondition { Field = "AISessionId", Operator = DataFilterOperator.In, Values = ["session-0", "session-9"], IsJoinKey = true });
        var engine = Query(AIChatReportDataSource.VoiceUsageDataSet, "ItemId");
        engine.Conditions.Add(Condition("Engine", DataFilterOperator.In, "realtime"));

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var engineRows = Rows(await ReportDesignerTestServices.ReadAsync(source, engine, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["summary-2", "summary-1"], dateRows.Select(row => row["ItemId"]));
        Assert.Equal(["summary-0"], keyRows.Select(row => row["ItemId"]));
        Assert.Equal(["summary-2", "summary-0"], engineRows.Select(row => row["ItemId"]));
    }

    [Fact]
    public async Task Aggregate_VoiceUsage_GroupsByTheNameTheIndexKeeps()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = AIChatReportDataSource.VoiceUsageDataSet,
            Groups = [new DataAggregateGroup { Field = "AIProfileName" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Count },
                new DataAggregateMeasure { Field = "AssistantSpeakingMs", Function = DataAggregate.Sum },
            ],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(AIChatReportDataSource.VoiceUsageDataSet, "AIProfileName"), TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (Count: (long)row[1], Speaking: Convert.ToInt64(row[2])));
        Assert.Equal((2L, 120_000L), counts["Voice agent"]);
        Assert.Equal((1L, 60_000L), counts[(string)rows[0]["AIProfileName"]]);
    }

    [Fact]
    public async Task ReportIndexMigrations_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var completions = await new AICompletionUsageIndexMigrations(_storeOptions) { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var voice = await new AIVoiceSessionSummaryIndexMigrations(_storeOptions) { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, completions);
        Assert.Equal(2, voice);
    }

    [Fact]
    public async Task Startups_ListTheUsageDataSets_OnlyWhenTheAnalyticsFeatureRegistersThem()
    {
        // Arrange
        await using var session = _store.CreateSession();
        await using var withoutAnalytics = Services(session, analytics: false);
        await using var withAnalytics = Services(session, analytics: true);
        var context = new DataSourceContext { User = _user };

        // Act
        var hidden = await withoutAnalytics.GetRequiredService<AIChatReportDataSource>().GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var listed = await withAnalytics.GetRequiredService<AIChatReportDataSource>().GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var usage = Rows(await ReportDesignerTestServices.ReadAsync(withAnalytics.GetRequiredService<AIChatReportDataSource>(), Query(AIChatReportDataSource.VoiceUsageDataSet, "ItemId"), TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain(hidden, dataSet => dataSet.Name is AIChatReportDataSource.CompletionUsageDataSet or AIChatReportDataSource.VoiceUsageDataSet);
        Assert.Contains(listed, dataSet => dataSet.Name == AIChatReportDataSource.CompletionUsageDataSet);
        Assert.Contains(listed, dataSet => dataSet.Name == AIChatReportDataSource.VoiceUsageDataSet);
        Assert.Equal(3, usage.Count);
    }

    private static ServiceProvider Services(ISession session, bool analytics)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(ProfileManager().Object);
        services.AddSingleton(Authorization(canViewAnalytics: true));
        services.AddSingleton(_storeOptions);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        new CrestApps.OrchardCore.AI.Chat.DataSourcesStartup().ConfigureServices(services);

        if (analytics)
        {
            new CrestApps.OrchardCore.AI.Chat.ChatAnalyticsDataSourcesStartup().ConfigureServices(services);
        }

        return services.BuildServiceProvider();
    }

    private static AIChatReportDataSource Source(ISession session, bool canViewAnalytics = true)
    {
        var authorizationService = Authorization(canViewAnalytics);

        return new AIChatReportDataSource(
            [
                new AICompletionUsageReportDataSet(session, ProfileManager().Object, authorizationService, _storeOptions, new PassThroughStringLocalizer<AICompletionUsageReportDataSet>()),
                new AIVoiceUsageReportDataSet(session, authorizationService, _storeOptions, new PassThroughStringLocalizer<AIVoiceUsageReportDataSet>()),
            ]);
    }

    private static IAuthorizationService Authorization(bool canViewAnalytics)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canViewAnalytics && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == ChatAnalyticsPermissionProvider.ViewChatAnalytics.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static Mock<IAIProfileManager> ProfileManager()
    {
        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile[]
            {
                new() { ItemId = "profile-support", Name = "support", DisplayText = "Support" },
                new() { ItemId = "profile-sales", Name = "Sales" },
            });

        return profileManager;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return Query(dataSet, _user, fields);
    }

    private static DataSourceQuery Query(string dataSet, ClaimsPrincipal user, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
