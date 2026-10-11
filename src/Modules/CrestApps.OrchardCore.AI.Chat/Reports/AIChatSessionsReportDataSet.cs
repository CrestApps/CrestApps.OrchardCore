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
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>ChatSessions</c> data set: one row per AI chat session. Only principals allowed to view AI chat analytics
/// read it. The client's remote address and its hash are never exposed.
/// </summary>
public sealed class AIChatSessionsReportDataSet : ReportRecordDataSet<AIChatReportRecord<AIChatSession>>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the last activity field, which reads are narrowed by.
    /// </summary>
    public const string LastActivityUtcField = "LastActivityUtc";

    private readonly ISession _session;
    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly YesSqlStoreOptions _storeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session sessions are read from.</param>
    /// <param name="profileManager">The AI profile manager, which names the profiles.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionsReportDataSet(
        ISession session,
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IOptions<YesSqlStoreOptions> storeOptions,
        IStringLocalizer<AIChatSessionsReportDataSet> stringLocalizer)
        : base(new ReportDataSetDescriptor(
            AIChatReportDataSource.ChatSessionsDataSet,
            stringLocalizer["Chat sessions"],
            stringLocalizer["One row per AI chat session."]))
    {
        _session = session;
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _storeOptions = storeOptions.Value;

        var S = stringLocalizer;
        var details = S["Session"].Value;
        var activity = S["Activity"].Value;
        var processing = S["Processing"].Value;

        AddField(AIChatReportDataSource.SessionIdField, S["Session ID"], ReportDataType.Text, record => record.Document.SessionId, details, isIdentifier: true);
        AddField("ProfileId", S["Profile ID"], ReportDataType.Text, record => record.Document.ProfileId, details, isIdentifier: true);
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], ReportDataType.Text, record => record.ProfileName, details);
        AddField("Title", S["Title"], ReportDataType.Text, record => record.Document.Title, details);
        AddField(
            ReportsConstants.UserIdField,
            S["User ID"],
            ReportDataType.Text,
            record => record.Document.UserId,
            details,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("Status", S["Status"], ReportDataType.Text, record => record.Document.Status, details);
        AddField("ResponseHandlerName", S["Response handler"], ReportDataType.Text, record => record.Document.ResponseHandlerName, details);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.Document.CreatedUtc, activity);
        AddField("ModifiedUtc", S["Modified"], ReportDataType.DateTime, record => record.Document.ModifiedUtc, activity);
        AddField(LastActivityUtcField, S["Last activity"], ReportDataType.DateTime, record => record.Document.LastActivityUtc, activity);
        AddField("ClosedAtUtc", S["Closed"], ReportDataType.DateTime, record => record.Document.ClosedAtUtc, activity);
        AddField("PostSessionProcessingStatus", S["Post-session processing"], ReportDataType.Text, record => record.Document.PostSessionProcessingStatus, processing);
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public override string DefaultDateField => LastActivityUtcField;

    public override async Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ChatAnalyticsPermissionProvider.ViewChatAnalytics);
    }

    /// <inheritdoc/>
    protected override IEnumerable<string> KeyFilterableFields => ["SessionId", "UserId"];

    /// <inheritdoc/>
    protected override async Task<IEnumerable<AIChatReportRecord<AIChatSession>>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var (from, to) = ReportDateRange.For(query.Conditions, LastActivityUtcField);
        var sessions = _session.Query<AIChatSession, AIChatSessionIndex>(collection: _storeOptions.AICollectionName);

        foreach (var field in KeyFilterableFields)
        {
            if (ReportJoinKeys.For(query.Conditions, field) is not { } keys)
            {
                continue;
            }

            if (keys.Count == 0)
            {
                return [];
            }

            var values = keys.ToArray();

            sessions = field == "SessionId"
                ? sessions.Where(index => index.SessionId.IsIn(values))
                : sessions.Where(index => index.UserId.IsIn(values));
        }

        if (from.HasValue)
        {
            var earliest = from.Value;
            sessions = sessions.Where(index => index.LastActivityUtc >= earliest);
        }

        if (to.HasValue)
        {
            var latest = to.Value;
            sessions = sessions.Where(index => index.LastActivityUtc <= latest);
        }

        var documents = await sessions
            .OrderByDescending(index => index.LastActivityUtc)
            .ThenByDescending(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);
        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        return documents
            .Select(document => new AIChatReportRecord<AIChatSession>(document, AIChatReportProfileNames.Find(profileNames, document.ProfileId)))
            .ToArray();
    }
}
