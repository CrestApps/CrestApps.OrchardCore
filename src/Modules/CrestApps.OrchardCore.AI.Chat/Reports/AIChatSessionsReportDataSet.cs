using System.Runtime.CompilerServices;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>ChatSessions</c> data set: one row per AI chat session. Only principals allowed to view AI chat analytics
/// read it. The client's remote address and its hash are never exposed.
/// </summary>
public sealed class AIChatSessionsReportDataSet : RecordDataSet<AIChatReportRecord<AIChatSession>>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the last activity field, which reads are narrowed by.
    /// </summary>
    public const string LastActivityUtcField = "LastActivityUtc";

    // The index columns a join can narrow the read on.
    private static readonly string[] _keyFields = [AIChatReportDataSource.SessionIdField, "UserId"];

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
        : base(new DataSetDescriptor(
            AIChatReportDataSource.ChatSessionsDataSet,
            stringLocalizer["Chat sessions"],
            stringLocalizer["One row per AI chat session."])
        {
            DefaultDateField = LastActivityUtcField,
        })
    {
        _session = session;
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _storeOptions = storeOptions.Value;

        var S = stringLocalizer;
        var details = S["Session"].Value;
        var activity = S["Activity"].Value;
        var processing = S["Processing"].Value;

        AddField(AIChatReportDataSource.SessionIdField, S["Session ID"], DataFieldType.Text, record => record.Document.SessionId, details).Identifier();
        AddField("ProfileId", S["Profile ID"], DataFieldType.Text, record => record.Document.ProfileId, details).Identifier();
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], DataFieldType.Text, record => record.ProfileName, details);
        AddField("Title", S["Title"], DataFieldType.Text, record => record.Document.Title, details);
        AddField(
            ReportsConstants.UserIdField,
            S["User ID"],
            DataFieldType.Text,
            record => record.Document.UserId, details).Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Document.Status, details);
        AddField("ResponseHandlerName", S["Response handler"], DataFieldType.Text, record => record.Document.ResponseHandlerName, details);
        AddField("ProfileVersion", S["Profile version"], DataFieldType.Integer, record => record.Document.TryGet<AIProfileVersionMetadata>(out var version) ? version.Version : (int?)null, details);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.Document.CreatedUtc, activity);
        AddField("ModifiedUtc", S["Modified"], DataFieldType.DateTime, record => record.Document.ModifiedUtc, activity);
        AddField(LastActivityUtcField, S["Last activity"], DataFieldType.DateTime, record => record.Document.LastActivityUtc, activity);
        AddField("ClosedAtUtc", S["Closed"], DataFieldType.DateTime, record => record.Document.ClosedAtUtc, activity);
        AddField("PostSessionProcessingStatus", S["Post-session processing"], DataFieldType.Text, record => record.Document.PostSessionProcessingStatus, processing);

        Fields.MarkKeyFilterable(_keyFields);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ChatAnalyticsPermissionProvider.ViewChatAnalytics);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AIChatReportRecord<AIChatSession>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, LastActivityUtcField);
        var keyFilters = new List<(string Field, string[] Values)>();

        foreach (var field in _keyFields)
        {
            if (DataConditions.Keys(query.Conditions, field) is not { } keys)
            {
                continue;
            }

            if (keys.Count == 0)
            {
                yield break;
            }

            keyFilters.Add((field, keys.Select(key => key?.ToString()).ToArray()));
        }

        IQueryIndex<AIChatSessionIndex> Filter(IQueryIndex<AIChatSessionIndex> sessions)
        {
            foreach (var (field, values) in keyFilters)
            {
                sessions = field == AIChatReportDataSource.SessionIdField
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

            return sessions;
        }

        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        await foreach (var document in ReportIndexPages.ReadAsync<AIChatSession, AIChatSessionIndex>(
            _session,
            Filter,
            index => index.LastActivityUtc,
            _storeOptions.AICollectionName,
            cancellationToken))
        {
            yield return new AIChatReportRecord<AIChatSession>(document, AIChatReportProfileNames.Find(profileNames, document.ProfileId));
        }
    }

}
