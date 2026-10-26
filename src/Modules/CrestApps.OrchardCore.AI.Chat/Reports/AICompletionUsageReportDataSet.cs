using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>CompletionUsage</c> data set: one row per request sent to an AI provider, of every kind and from every
/// feature, with every unit the provider bills in, so a report can price the requests per model. The AI Chat Session
/// Analytics feature records the requests while AI usage tracking is on. Only principals allowed to view AI chat
/// analytics read it, as on the AI Usage Analytics page. The client identifier, which can identify a browser or a
/// device, and the provider's response identifier are never exposed.
/// </summary>
public sealed class AICompletionUsageReportDataSet : IndexedReportDataSet<AICompletionUsageRecord, AICompletionUsageIndex, AIChatReportRecord<AICompletionUsageRecord>>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the field that holds when the request was recorded, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// The technical name of the field that holds the model a request is priced by: the model the provider reported,
    /// or the deployment when it reported none.
    /// </summary>
    public const string ModelField = "Model";

    private const decimal MillisecondsPerMinute = 60_000m;

    private readonly IAIProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionUsageReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the usage records are read from.</param>
    /// <param name="profileManager">The AI profile manager, which names the profiles.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICompletionUsageReportDataSet(
        ISession session,
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IOptions<YesSqlStoreOptions> storeOptions,
        IStringLocalizer<AICompletionUsageReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                AIChatReportDataSource.CompletionUsageDataSet,
                stringLocalizer["AI completion usage"],
                stringLocalizer["One row per request sent to an AI provider: its kind, category and purpose, the model, deployment and connection, the profile, session and user, and every unit the provider bills in."]),
            session,
            authorizationService,
            storeOptions.Value.AICollectionName,
            ChatAnalyticsPermissionProvider.ViewChatAnalytics)
    {
        _profileManager = profileManager;

        var S = stringLocalizer;
        var request = S["Request"].Value;
        var model = S["Model"].Value;
        var context = S["Context"].Value;
        var billing = S["Billing units"].Value;

        AddField(CreatedUtcField, S["Recorded"], DataFieldType.DateTime, record => record.Document.CreatedUtc, request);
        AddField(nameof(AICompletionUsageRecord.OperationType), S["Kind"], DataFieldType.Text, record => AIUsageReport.GetOperationType(record.Document), request)
            .Description = S["Chat, embeddings, images, speech-to-text, text-to-speech, realtime voice or realtime transcription. A request recorded before kinds were recorded is a chat request."].Value;
        AddField(nameof(AICompletionUsageRecord.ContextType), S["Category"], DataFieldType.Text, record => record.Document.ContextType, request)
            .Description = S["The feature that made the request, such as chat sessions, chat interactions, SMS or voice."].Value;
        AddField(nameof(AICompletionUsageRecord.Purpose), S["Purpose"], DataFieldType.Text, record => record.Document.Purpose, request)
            .Description = S["Why the request was made within its category."].Value;
        AddField(nameof(AICompletionUsageRecord.IsStreaming), S["Streamed"], DataFieldType.Boolean, record => record.Document.IsStreaming, request);
        AddField(nameof(AICompletionUsageRecord.ResponseLatencyMs), S["Response latency (ms)"], DataFieldType.Decimal, record => record.Document.ResponseLatencyMs, request);

        AddField(nameof(AICompletionUsageRecord.ClientName), S["Provider"], DataFieldType.Text, record => record.Document.ClientName, model);
        AddField(nameof(AICompletionUsageRecord.ConnectionName), S["Connection"], DataFieldType.Text, record => record.Document.ConnectionName, model);
        AddField(nameof(AICompletionUsageRecord.DeploymentName), S["Deployment"], DataFieldType.Text, record => record.Document.DeploymentName, model);
        AddField(nameof(AICompletionUsageRecord.ModelName), S["Model reported"], DataFieldType.Text, record => record.Document.ModelName, model)
            .Description = S["The model the provider reported. Empty when the provider reported none."].Value;
        AddField(ModelField, S["Model"], DataFieldType.Text, record => GetModel(record.Document), model)
            .Description = S["The model the provider reported, or the deployment when it reported none. Price the requests by this field."].Value;

        AddField(nameof(AICompletionUsageRecord.ProfileId), S["Profile ID"], DataFieldType.Text, record => record.Document.ProfileId, context).Identifier();
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], DataFieldType.Text, record => record.ProfileName, context);
        AddField(AIChatReportDataSource.SessionIdField, S["Chat session ID"], DataFieldType.Text, record => record.Document.SessionId, context)
            .Identifier(new DataFieldReference(AIChatReportDataSource.SourceName, AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField));
        AddField(nameof(AICompletionUsageRecord.InteractionId), S["Chat interaction ID"], DataFieldType.Text, record => record.Document.InteractionId, context).Identifier();
        AddField(nameof(AICompletionUsageRecord.UserId), S["User ID"], DataFieldType.Text, record => record.Document.UserId, context).Identifier(ReportDataReferences.User());
        AddField(nameof(AICompletionUsageRecord.UserName), S["User name"], DataFieldType.Text, record => record.Document.UserName, context);
        AddField(nameof(AICompletionUsageRecord.IsAuthenticated), S["Authenticated"], DataFieldType.Boolean, record => record.Document.IsAuthenticated, context);

        AddField(nameof(AICompletionUsageRecord.InputTokenCount), S["Input tokens"], DataFieldType.Integer, record => record.Document.InputTokenCount, billing);
        AddField(nameof(AICompletionUsageRecord.OutputTokenCount), S["Output tokens"], DataFieldType.Integer, record => record.Document.OutputTokenCount, billing);
        AddField(nameof(AICompletionUsageRecord.TotalTokenCount), S["Total tokens"], DataFieldType.Integer, record => record.Document.TotalTokenCount, billing);
        AddField(nameof(AICompletionUsageRecord.CachedInputTokenCount), S["Cached input tokens"], DataFieldType.Integer, record => record.Document.CachedInputTokenCount, billing)
            .Description = S["Input tokens the provider served from its prompt cache, usually billed at a discount."].Value;
        AddField(nameof(AICompletionUsageRecord.ReasoningTokenCount), S["Reasoning tokens"], DataFieldType.Integer, record => record.Document.ReasoningTokenCount, billing)
            .Description = S["Output tokens a reasoning model spent before it answered."].Value;
        AddField(nameof(AICompletionUsageRecord.InputAudioTokenCount), S["Audio input tokens"], DataFieldType.Integer, record => record.Document.InputAudioTokenCount, billing);
        AddField(nameof(AICompletionUsageRecord.OutputAudioTokenCount), S["Audio output tokens"], DataFieldType.Integer, record => record.Document.OutputAudioTokenCount, billing);
        AddField(nameof(AICompletionUsageRecord.AudioDurationMs), S["Audio transcribed (ms)"], DataFieldType.Integer, record => record.Document.AudioDurationMs, billing);
        AddField("AudioMinutes", S["Audio transcribed (minutes)"], DataFieldType.Decimal, record => record.Document.AudioDurationMs / MillisecondsPerMinute, billing)
            .Description = S["For speech-to-text models billed by the minute."].Value;
        AddField(nameof(AICompletionUsageRecord.CharacterCount), S["Characters synthesized"], DataFieldType.Integer, record => record.Document.CharacterCount, billing)
            .Description = S["For text-to-speech models billed by the character."].Value;
        AddField(nameof(AICompletionUsageRecord.ImageCount), S["Images generated"], DataFieldType.Integer, record => record.Document.ImageCount, billing)
            .Description = S["For image models billed by the image."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<AICompletionUsageIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AICompletionUsageIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AICompletionUsageIndex, string>>>(StringComparer.Ordinal)
        {
            [AIChatReportDataSource.SessionIdField] = index => index.SessionId,
            [nameof(AICompletionUsageRecord.ProfileId)] = index => index.ProfileId,
            [nameof(AICompletionUsageRecord.InteractionId)] = index => index.InteractionId,
            [nameof(AICompletionUsageRecord.UserId)] = index => index.UserId,
        };

    /// <summary>
    /// Gets a value indicating whether the data set can group and aggregate itself in the database. It can: each
    /// record gives one row, and every principal that may read the data set reads every record.
    /// </summary>
    protected override bool CanAggregate => true;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the latency as a floating-point number, which the database adds up differently from the
        // consumer's decimals, so it is left to the consumer.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(AICompletionUsageRecord.ResponseLatencyMs));

        return columns;
    }

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<AICompletionUsageIndex> columns)
    {
        columns
            .Text(nameof(AICompletionUsageRecord.ContextType), nameof(AICompletionUsageIndex.ContextType))
            .Text(nameof(AICompletionUsageRecord.ClientName), nameof(AICompletionUsageIndex.ClientName))
            .Text(nameof(AICompletionUsageRecord.ConnectionName), nameof(AICompletionUsageIndex.ConnectionName))
            .Text(nameof(AICompletionUsageRecord.DeploymentName), nameof(AICompletionUsageIndex.DeploymentName))
            .Text(nameof(AICompletionUsageRecord.ModelName), nameof(AICompletionUsageIndex.ModelName));
    }

    /// <inheritdoc/>
    protected override IEnumerable<AIChatReportRecord<AICompletionUsageRecord>> GetRows(AICompletionUsageRecord document)
    {
        yield return new AIChatReportRecord<AICompletionUsageRecord>(document, null);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AIChatReportRecord<AICompletionUsageRecord>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            yield return new AIChatReportRecord<AICompletionUsageRecord>(row.Document, AIChatReportProfileNames.Find(profileNames, row.Document.ProfileId));
        }
    }

    // A provider that reports no model leaves the name empty rather than null, so fall back on the deployment.
    private static string GetModel(AICompletionUsageRecord record)
    {
        return string.IsNullOrEmpty(record.ModelName)
            ? record.DeploymentName
            : record.ModelName;
    }
}
