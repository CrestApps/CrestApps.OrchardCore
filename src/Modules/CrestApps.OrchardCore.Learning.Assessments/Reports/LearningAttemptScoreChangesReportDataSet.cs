using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Assessments.Reports;

/// <summary>
/// One row per change of an attempt's score: submitted, graded, overridden, regraded or voided, with who and why.
/// </summary>
public sealed class LearningAttemptScoreChangesReportDataSet : IndexedReportDataSet<AssessmentAttempt, AssessmentAttemptIndex, ReportChildRow<AssessmentAttempt, AttemptScoreChange>>, IAssessmentsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningAttemptScoreChanges";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAttemptScoreChangesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningAttemptScoreChangesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningAttemptScoreChangesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Quiz score changes"], stringLocalizer["One row per change of an attempt's score: submitted, graded, overridden, regraded or voided, with who changed it and why."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Score change"].Value;

        AddField("AttemptId", S["Attempt ID"], DataFieldType.Text, row => row.Parent.ItemId, group).Identifier(new DataFieldReference(AssessmentsConstants.DataSourceName, LearningAttemptsReportDataSet.DataSetName, "AttemptId"));
        AddField("UserId", S["Learner"], DataFieldType.Text, row => row.Parent.UserId, group).Identifier(ReportDataReferences.User());
        AddField("QuizId", S["Quiz"], DataFieldType.Text, row => row.Parent.QuizId, group).Identifier(ReportDataReferences.ContentItem(AssessmentsConstants.ContentTypes.Quiz));
        AddField("ChangedUtc", S["Changed"], DataFieldType.DateTime, row => row.Child.ChangedUtc, group);
        AddField("Kind", S["Change"], DataFieldType.Text, row => row.Child.Kind, group);
        AddField("From", S["From"], DataFieldType.Decimal, row => row.Child.From, group);
        AddField("To", S["To"], DataFieldType.Decimal, row => row.Child.To, group);
        AddField("Actor", S["Changed by"], DataFieldType.Text, row => row.Child.Actor, group).Identifier(ReportDataReferences.User());
        AddField("Reason", S["Reason"], DataFieldType.Text, row => row.Child.Reason, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssessmentAttemptIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssessmentAttemptIndex, string>>>(StringComparer.Ordinal)
        {
            ["AttemptId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["QuizId"] = index => index.QuizId,
        };

    /// <inheritdoc />
    protected override IEnumerable<ReportChildRow<AssessmentAttempt, AttemptScoreChange>> GetRows(AssessmentAttempt document)
        => ReportChildRow<AssessmentAttempt, AttemptScoreChange>.For(document, document.ScoreHistory);
}
