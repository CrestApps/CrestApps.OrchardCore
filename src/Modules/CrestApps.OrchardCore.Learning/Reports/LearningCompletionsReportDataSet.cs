using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// One row per completed enrollment, from its completion record: the course as it was, when, the grade and the time
/// spent, and who completed it by hand.
/// </summary>
public sealed class LearningCompletionsReportDataSet : IndexedReportDataSet<Enrollment, EnrollmentIndex>, ILearningReportDataSet
{
    /// <summary>
    /// The stable name of the data set.
    /// </summary>
    public const string DataSetName = "LearningCompletions";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCompletionsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningCompletionsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningCompletionsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Completions"], stringLocalizer["One row per completed enrollment, as its completion record kept it: the course, when, the grade, the time spent and who completed it by hand."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Completion"].Value;

        AddField("EnrollmentId", S["Enrollment ID"], DataFieldType.Text, record => record.ItemId, group).Identifier(new DataFieldReference(LearningConstants.DataSourceName, LearningEnrollmentsReportDataSet.DataSetName, "ItemId"));
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("UserId", S["Learner"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("CourseId", S["Course"], DataFieldType.Text, record => record.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("CourseTitle", S["Course title at completion"], DataFieldType.Text, record => record.Completion?.CourseTitle, group);
        AddField("CourseCode", S["Course code at completion"], DataFieldType.Text, record => record.Completion?.CourseCode, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.Completion?.CompletedUtc ?? record.CompletedUtc, group);
        AddField("Grade", S["Grade (%)"], DataFieldType.Decimal, record => record.Completion?.Grade, group);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, record => record.Completion?.Passed, group);
        AddField("SecondsSpent", S["Time spent (seconds)"], DataFieldType.Integer, record => record.Completion?.SecondsSpent ?? 0, group);
        AddField("Cycle", S["Pass"], DataFieldType.Integer, record => record.Cycle, group);
        AddField("CompletedBy", S["Completed by hand by"], DataFieldType.Text, record => record.Completion?.CompletedBy, group).Identifier(ReportDataReferences.User());
        AddField("Reason", S["Reason"], DataFieldType.Text, record => record.Completion?.Reason, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<EnrollmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<EnrollmentIndex, string>>>(StringComparer.Ordinal)
        {
            ["EnrollmentId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["CourseId"] = index => index.CourseId,
        };

    /// <inheritdoc />
    protected override bool CanAggregate => false;

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<EnrollmentIndex>, IQueryIndex<EnrollmentIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var restrict = await base.RestrictAsync(context, cancellationToken);

        return restrict is null ? null : rows => restrict(rows).Where(index => index.Status == EnrollmentStatus.Completed);
    }
}
