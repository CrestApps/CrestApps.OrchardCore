using System.Runtime.CompilerServices;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Indexes;
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
/// The <c>ConversationEvaluations</c> data set: one row per AI conversation evaluation, real or simulated, with its
/// score, whether it passed and the problems found. Only principals allowed to view AI chat analytics read it.
/// </summary>
public sealed class AIConversationEvaluationsReportDataSet : RecordDataSet<AIChatReportRecord<AIConversationEvaluation>>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the evaluation date field, which reads are narrowed by.
    /// </summary>
    public const string EvaluatedUtcField = "EvaluatedUtc";

    private readonly ISession _session;
    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly YesSqlStoreOptions _storeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluationsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the evaluations are read from.</param>
    /// <param name="profileManager">The AI profile manager, which names the profiles.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationEvaluationsReportDataSet(
        ISession session,
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IOptions<YesSqlStoreOptions> storeOptions,
        IStringLocalizer<AIConversationEvaluationsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            AIChatReportDataSource.ConversationEvaluationsDataSet,
            stringLocalizer["Conversation evaluations"],
            stringLocalizer["One row per AI conversation evaluation, real or simulated, with its score and the problems found."])
        {
            DefaultDateField = EvaluatedUtcField,
        })
    {
        _session = session;
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _storeOptions = storeOptions.Value;

        var S = stringLocalizer;
        var details = S["Evaluation"].Value;
        var result = S["Result"].Value;
        var issues = S["Issues"].Value;

        AddField("ItemId", S["Evaluation ID"], DataFieldType.Text, record => record.Document.ItemId, details).Identifier();
        AddField(
            AIChatReportDataSource.SessionIdField,
            S["Session ID"],
            DataFieldType.Text,
            record => record.Document.SessionId, details).Identifier(new DataFieldReference(AIChatReportDataSource.SourceName, AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField));
        AddField("ProfileId", S["Profile ID"], DataFieldType.Text, record => record.Document.ProfileId, details).Identifier();
        AddField(AIChatReportProfileNames.ProfileNameField, S["Profile"], DataFieldType.Text, record => record.ProfileName, details);
        AddField("ProfileVersion", S["Profile version"], DataFieldType.Integer, record => record.Document.ProfileVersion, details);
        AddField("Label", S["Label"], DataFieldType.Text, record => record.Document.Label, details);
        AddField("Simulated", S["Simulated"], DataFieldType.Boolean, record => record.Document.SessionId is null, details);
        AddField(EvaluatedUtcField, S["Evaluated"], DataFieldType.DateTime, record => record.Document.EvaluatedUtc, details);
        AddField("Score", S["Score"], DataFieldType.Integer, record => record.Document.Score, result);
        AddField("PassingScore", S["Passing score"], DataFieldType.Integer, record => record.Document.PassingScore, result);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, record => record.Document.Passed, result);
        AddField("Resolved", S["Resolved"], DataFieldType.Boolean, record => record.Document.Resolved, result);
        AddField("Sentiment", S["Sentiment"], DataFieldType.Text, record => record.Document.Sentiment, result);
        AddField("Summary", S["Summary"], DataFieldType.Text, record => record.Document.Summary, result);
        AddField("CriteriaMet", S["Criteria met"], DataFieldType.Integer, record => record.Document.Criteria.Count(criterion => criterion.Met), result);
        AddField("CriteriaCount", S["Criteria"], DataFieldType.Integer, record => record.Document.Criteria.Count, result);
        AddField("IssueCount", S["Issues"], DataFieldType.Integer, record => record.Document.Issues.Count, issues);

        foreach (var kind in AIConversationIssueKinds.All)
        {
            var issueKind = kind;
            AddField(issueKind + "Issues", S["{0} issues", issueKind], DataFieldType.Integer, record => record.Document.Issues.Count(issue => issue.Kind == issueKind), issues);
        }
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, ChatAnalyticsPermissionProvider.ViewChatAnalytics);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AIChatReportRecord<AIConversationEvaluation>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, EvaluatedUtcField);

        IQueryIndex<AIConversationEvaluationIndex> Filter(IQueryIndex<AIConversationEvaluationIndex> evaluations)
        {
            if (from.HasValue)
            {
                var earliest = from.Value;
                evaluations = evaluations.Where(index => index.EvaluatedUtc >= earliest);
            }

            if (to.HasValue)
            {
                var latest = to.Value;
                evaluations = evaluations.Where(index => index.EvaluatedUtc <= latest);
            }

            return evaluations;
        }

        var profileNames = await AIChatReportProfileNames.LoadAsync(_profileManager, query, cancellationToken);

        await foreach (var document in ReportIndexPages.ReadAsync<AIConversationEvaluation, AIConversationEvaluationIndex>(
            _session,
            Filter,
            index => index.EvaluatedUtc,
            _storeOptions.AICollectionName,
            cancellationToken))
        {
            yield return new AIChatReportRecord<AIConversationEvaluation>(document, AIChatReportProfileNames.Find(profileNames, document.ProfileId));
        }
    }
}
