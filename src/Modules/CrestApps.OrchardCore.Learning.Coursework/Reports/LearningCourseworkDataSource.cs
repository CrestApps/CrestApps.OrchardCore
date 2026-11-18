using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Coursework.Reports;

/// <summary>
/// The Learning coursework data source of Data Pipelines and the Report Builder.
/// </summary>
public sealed class LearningCourseworkDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCourseworkDataSource"/> class.
    /// </summary>
    /// <param name="submissions">The submissions data set.</param>
    public LearningCourseworkDataSource(SubmissionReportDataSet submissions)
    {
        _dataSets = [submissions];
    }

    /// <summary>Gets the name.</summary>
    public static string Name => CourseworkConstants.DataSourceName;

    /// <summary>Gets the display name.</summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningCourseworkDataSource>("Learning coursework");

    /// <summary>Gets the description.</summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningCourseworkDataSource>("The coursework learners handed in, when, and how it was graded.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per submission, without the text, the files or the feedback; people are user ids, never e-mail addresses.
/// </summary>
public sealed class SubmissionReportDataSet : IndexedReportDataSet<Submission, SubmissionIndex>
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningSubmissions";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubmissionReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<SubmissionReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Submissions"], stringLocalizer["One row per hand-in of coursework, with its state and grade."]), session, authorizationService, CourseworkConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Submission"].Value;

        AddField("SubmissionId", S["Submission ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("EnrollmentId", S["Enrollment"], DataFieldType.Text, record => record.EnrollmentId, group).Identifier();
        AddField("UserId", S["Learner"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("CourseId", S["Course"], DataFieldType.Text, record => record.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("CourseworkId", S["Coursework"], DataFieldType.Text, record => record.CourseworkId, group).Identifier(ReportDataReferences.ContentItem(CourseworkConstants.ContentTypes.Coursework));
        AddField("CourseworkTitle", S["Coursework title"], DataFieldType.Text, record => record.CourseworkTitle, group);
        AddField("Attempt", S["Attempt"], DataFieldType.Integer, record => record.Attempt, group);
        AddField("Current", S["Latest attempt"], DataFieldType.Boolean, record => record.Current, group);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("Late", S["Late"], DataFieldType.Boolean, record => record.Late, group);
        AddField("SubmittedUtc", S["Handed in"], DataFieldType.DateTime, record => record.SubmittedUtc, group);
        AddField("FileCount", S["Files"], DataFieldType.Integer, record => record.Files?.Count ?? 0, group);
        AddField("Score", S["Grade (%)"], DataFieldType.Decimal, record => record.Score, group);
        AddField("RawScore", S["Grade before the late penalty (%)"], DataFieldType.Decimal, record => record.RawScore, group);
        AddField("LatePenaltyPercent", S["Late penalty (%)"], DataFieldType.Decimal, record => record.LatePenaltyPercent, group);
        AddField("DaysLate", S["Days late"], DataFieldType.Integer, record => record.DaysLate, group);
        AddField("Points", S["Points"], DataFieldType.Decimal, record => record.Points, group);
        AddField("MaxPoints", S["Out of"], DataFieldType.Decimal, record => record.MaxPoints, group);
        AddField("GraderId", S["Grader"], DataFieldType.Text, record => record.GraderId, group).Identifier(ReportDataReferences.User());
        AddField("GradedUtc", S["Graded"], DataFieldType.DateTime, record => record.GradedUtc, group);
        AddField("Regrades", S["Regrades"], DataFieldType.Integer, record => record.History?.Count ?? 0, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<SubmissionIndex, DateTime>> Column)? DateColumn
        => ("SubmittedUtc", index => index.SubmittedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<SubmissionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<SubmissionIndex, string>>>(StringComparer.Ordinal)
        {
            ["SubmissionId"] = index => index.ItemId,
            ["EnrollmentId"] = index => index.EnrollmentId,
            ["UserId"] = index => index.UserId,
            ["CourseId"] = index => index.CourseId,
            ["CourseworkId"] = index => index.CourseworkId,
        };
}
