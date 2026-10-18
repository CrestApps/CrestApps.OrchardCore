using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>ChatSessionMetrics</c> data set: one row per AI chat session tracked by the AI Chat Session Analytics
/// feature, with its usage, latency, feedback, and conversion metrics. Only principals allowed to view AI chat
/// analytics read it. The client's remote address and its hash are never exposed.
/// </summary>
public sealed class AIChatSessionMetricsReportDataSet : RecordDataSet<AIChatReportRecord<AIChatSessionEvent>>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the session start field, which reads are narrowed by.
    /// </summary>
    public const string SessionStartedUtcField = "SessionStartedUtc";

    private readonly ISession _session;
    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly YesSqlStoreOptions _storeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionMetricsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the metrics are read from.</param>
    /// <param name="profileManager">The AI profile manager, which names the profiles.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionMetricsReportDataSet(
        ISession session,
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IOptions<YesSqlStoreOptions> storeOptions,
        IStringLocalizer<AIChatSessionMetricsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            AIChatReportDataSource.ChatSessionMetricsDataSet,
            stringLocalizer["Chat session metrics"],
            stringLocalizer["One row per tracked AI chat session, with its usage, feedback, and conversion metrics."])
        {
            DefaultDateField = SessionStartedUtcField,
        })
    {
        _session = session;
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _storeOptions = storeOptions.Value;

        var S = stringLocalizer;
        var details = S["Session"].Value;
        var usage = S["Usage"].Value;
        var feedback = S["Feedback"].Value;
        var conversion = S["Conversion"].Value;

        AddField(
            AIChatReportDataSource.SessionIdField,
            S["Session ID"],
            DataFieldType.Text,
            record => record.Document.SessionId, details).Identifier(new DataFieldReference(AIChatReportDataSource.SourceName, AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField));
        AddField("ProfileId", S["Profile ID"], DataFieldType.Text, record => record.Document.ProfileId, details).Identifier();
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], DataFieldType.Text, record => record.ProfileName, details);
        AddField("VisitorId", S["Visitor ID"], DataFieldType.Text, record => record.Document.VisitorId, details).Identifier();
        AddField(
            ReportsConstants.UserIdField,
            S["User ID"],
            DataFieldType.Text,
            record => record.Document.UserId, details).Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("IsAuthenticated", S["Authenticated"], DataFieldType.Boolean, record => record.Document.IsAuthenticated, details);
        AddField(SessionStartedUtcField, S["Started"], DataFieldType.DateTime, record => record.Document.SessionStartedUtc, details);
        AddField("SessionEndedUtc", S["Ended"], DataFieldType.DateTime, record => record.Document.SessionEndedUtc, details);
        AddField("IsResolved", S["Resolved"], DataFieldType.Boolean, record => record.Document.IsResolved, details);
        AddField("CreatedUtc", S["Recorded"], DataFieldType.DateTime, record => record.Document.CreatedUtc, details);
        AddField("MessageCount", S["Messages"], DataFieldType.Integer, record => record.Document.MessageCount, usage);
        AddField("HandleTimeSeconds", S["Handle time (seconds)"], DataFieldType.Decimal, record => record.Document.HandleTimeSeconds, usage);
        AddField("TotalInputTokens", S["Input tokens"], DataFieldType.Integer, record => record.Document.TotalInputTokens, usage);
        AddField("TotalOutputTokens", S["Output tokens"], DataFieldType.Integer, record => record.Document.TotalOutputTokens, usage);
        AddField("AverageResponseLatencyMs", S["Average response latency (ms)"], DataFieldType.Decimal, record => record.Document.AverageResponseLatencyMs, usage);
        AddField("CompletionCount", S["Completions"], DataFieldType.Integer, record => record.Document.CompletionCount, usage);
        AddField("UserRating", S["User rating"], DataFieldType.Boolean, record => record.Document.UserRating, feedback);
        AddField("ThumbsUpCount", S["Thumbs up"], DataFieldType.Integer, record => record.Document.ThumbsUpCount, feedback);
        AddField("ThumbsDownCount", S["Thumbs down"], DataFieldType.Integer, record => record.Document.ThumbsDownCount, feedback);
        AddField("ConversionScore", S["Conversion score"], DataFieldType.Integer, record => record.Document.ConversionScore, conversion);
        AddField("ConversionMaxScore", S["Conversion max score"], DataFieldType.Integer, record => record.Document.ConversionMaxScore, conversion);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ChatAnalyticsPermissionProvider.ViewChatAnalytics);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AIChatReportRecord<AIChatSessionEvent>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, SessionStartedUtcField);

        IQueryIndex<AIChatSessionMetricsIndex> Filter(IQueryIndex<AIChatSessionMetricsIndex> events)
        {
            if (from.HasValue)
            {
                var earliest = from.Value;
                events = events.Where(index => index.SessionStartedUtc >= earliest);
            }

            if (to.HasValue)
            {
                var latest = to.Value;
                events = events.Where(index => index.SessionStartedUtc <= latest);
            }

            return events;
        }

        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        await foreach (var document in ReportIndexPages.ReadAsync<AIChatSessionEvent, AIChatSessionMetricsIndex>(
            _session,
            Filter,
            index => index.SessionStartedUtc,
            _storeOptions.AICollectionName,
            cancellationToken))
        {
            yield return new AIChatReportRecord<AIChatSessionEvent>(document, AIChatReportProfileNames.Find(profileNames, document.ProfileId));
        }
    }

}
