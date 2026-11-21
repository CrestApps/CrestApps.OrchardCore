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
/// One row per quiz attempt.
/// </summary>
public sealed class LearningAttemptsReportDataSet : IndexedReportDataSet<AssessmentAttempt, AssessmentAttemptIndex>, IAssessmentsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningAttempts";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningAttemptsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningAttemptsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningAttemptsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Quiz attempts"], stringLocalizer["One row per quiz attempt: who, which quiz, when it started and was submitted, the score and whether it passed."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Attempt"].Value;

        AddField("AttemptId", S["Attempt ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("UserId", S["Learner"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("QuizId", S["Quiz"], DataFieldType.Text, record => record.QuizId, group).Identifier(ReportDataReferences.ContentItem(AssessmentsConstants.ContentTypes.Quiz));
        AddField("QuizTitle", S["Quiz title when started"], DataFieldType.Text, record => record.QuizTitle, group);
        AddField("CourseId", S["Course"], DataFieldType.Text, record => record.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("EnrollmentId", S["Enrollment ID"], DataFieldType.Text, record => record.EnrollmentId, group).Identifier(new DataFieldReference(LearningConstants.DataSourceName, "LearningEnrollments", "ItemId"));
        AddField("Sequence", S["Attempt number of the learner"], DataFieldType.Integer, record => record.Sequence, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("StartedUtc", S["Started"], DataFieldType.DateTime, record => record.StartedUtc, group);
        AddField("DeadlineUtc", S["Deadline"], DataFieldType.DateTime, record => record.DeadlineUtc, group);
        AddField("SubmittedUtc", S["Submitted"], DataFieldType.DateTime, record => record.SubmittedUtc, group);
        AddField("AutoSubmitted", S["Submitted when the time was up"], DataFieldType.Boolean, record => record.AutoSubmitted, group);
        AddField("Score", S["Score"], DataFieldType.Decimal, record => record.Score, group);
        AddField("MaxScore", S["Points"], DataFieldType.Decimal, record => record.MaxScore, group);
        AddField("Percent", S["Score (%)"], DataFieldType.Decimal, record => record.Percent, group);
        AddField("PassMark", S["Pass mark (%)"], DataFieldType.Decimal, record => record.PassMark, group);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, record => record.Passed, group);
        AddField("GradedBy", S["Graded by"], DataFieldType.Text, record => record.GradedBy, group).Identifier(ReportDataReferences.User());
        AddField("GradedUtc", S["Graded"], DataFieldType.DateTime, record => record.GradedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssessmentAttemptIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssessmentAttemptIndex, string>>>(StringComparer.Ordinal)
        {
            ["AttemptId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["QuizId"] = index => index.QuizId,
            ["CourseId"] = index => index.CourseId,
        };
}
