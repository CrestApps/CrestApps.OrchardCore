using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>ChatSessionMetrics</c> data set: one row per AI chat session tracked by the AI Chat Session Analytics
/// feature, with its usage, latency, feedback, and conversion metrics. Only principals allowed to view AI chat
/// analytics read it. The client's remote address and its hash are never exposed.
/// </summary>
public sealed class AIChatSessionMetricsReportDataSet : ReportRecordDataSet<AIChatReportRecord<AIChatSessionEvent>>, IAIChatReportDataSet
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
        : base(new ReportDataSetDescriptor(
            AIChatReportDataSource.ChatSessionMetricsDataSet,
            stringLocalizer["Chat session metrics"],
            stringLocalizer["One row per tracked AI chat session, with its usage, feedback, and conversion metrics."]))
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
            ReportDataType.Text,
            record => record.Document.SessionId,
            details,
            isIdentifier: true,
            new ReportFieldReference(AIChatReportDataSource.SourceName, AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField));
        AddField("ProfileId", S["Profile ID"], ReportDataType.Text, record => record.Document.ProfileId, details, isIdentifier: true);
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], ReportDataType.Text, record => record.ProfileName, details);
        AddField("VisitorId", S["Visitor ID"], ReportDataType.Text, record => record.Document.VisitorId, details, isIdentifier: true);
        AddField(
            ReportsConstants.UserIdField,
            S["User ID"],
            ReportDataType.Text,
            record => record.Document.UserId,
            details,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("IsAuthenticated", S["Authenticated"], ReportDataType.Boolean, record => record.Document.IsAuthenticated, details);
        AddField(SessionStartedUtcField, S["Started"], ReportDataType.DateTime, record => record.Document.SessionStartedUtc, details);
        AddField("SessionEndedUtc", S["Ended"], ReportDataType.DateTime, record => record.Document.SessionEndedUtc, details);
        AddField("IsResolved", S["Resolved"], ReportDataType.Boolean, record => record.Document.IsResolved, details);
        AddField("CreatedUtc", S["Recorded"], ReportDataType.DateTime, record => record.Document.CreatedUtc, details);
        AddField("MessageCount", S["Messages"], ReportDataType.Integer, record => record.Document.MessageCount, usage);
        AddField("HandleTimeSeconds", S["Handle time (seconds)"], ReportDataType.Decimal, record => record.Document.HandleTimeSeconds, usage);
        AddField("TotalInputTokens", S["Input tokens"], ReportDataType.Integer, record => record.Document.TotalInputTokens, usage);
        AddField("TotalOutputTokens", S["Output tokens"], ReportDataType.Integer, record => record.Document.TotalOutputTokens, usage);
        AddField("AverageResponseLatencyMs", S["Average response latency (ms)"], ReportDataType.Decimal, record => record.Document.AverageResponseLatencyMs, usage);
        AddField("CompletionCount", S["Completions"], ReportDataType.Integer, record => record.Document.CompletionCount, usage);
        AddField("UserRating", S["User rating"], ReportDataType.Boolean, record => record.Document.UserRating, feedback);
        AddField("ThumbsUpCount", S["Thumbs up"], ReportDataType.Integer, record => record.Document.ThumbsUpCount, feedback);
        AddField("ThumbsDownCount", S["Thumbs down"], ReportDataType.Integer, record => record.Document.ThumbsDownCount, feedback);
        AddField("ConversionScore", S["Conversion score"], ReportDataType.Integer, record => record.Document.ConversionScore, conversion);
        AddField("ConversionMaxScore", S["Conversion max score"], ReportDataType.Integer, record => record.Document.ConversionMaxScore, conversion);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ChatAnalyticsPermissionProvider.ViewChatAnalytics);
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<AIChatReportRecord<AIChatSessionEvent>>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var (from, to) = ReportDateRange.For(query.Conditions, SessionStartedUtcField);
        var events = _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(collection: _storeOptions.AICollectionName);

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

        var documents = await events
            .OrderByDescending(index => index.SessionStartedUtc)
            .ThenByDescending(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);
        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        return documents
            .Select(document => new AIChatReportRecord<AIChatSessionEvent>(document, AIChatReportProfileNames.Find(profileNames, document.ProfileId)))
            .ToArray();
    }
}
