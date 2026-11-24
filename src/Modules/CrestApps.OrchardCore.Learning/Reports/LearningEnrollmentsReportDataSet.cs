using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// One row per enrollment: learner, course, status, dates, progress and grade.
/// </summary>
public sealed class LearningEnrollmentsReportDataSet : IndexedReportDataSet<Enrollment, EnrollmentIndex>, ILearningReportDataSet
{
    /// <summary>
    /// The stable name of the data set.
    /// </summary>
    public const string DataSetName = "LearningEnrollments";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningEnrollmentsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningEnrollmentsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningEnrollmentsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Enrollments"], stringLocalizer["One row per enrollment: the learner, the course, the status, the dates, the progress and the grade."]) { DefaultDateField = "CreatedUtc" }, session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Enrollment"].Value;

        AddField("ItemId", S["Enrollment ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, group);
        AddField("UserId", S["Learner"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("CourseId", S["Course"], DataFieldType.Text, record => record.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("OfferingId", S["Offering"], DataFieldType.Text, record => record.OfferingId, group).Identifier();
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status.ToString(), group);
        AddField("Cycle", S["Pass"], DataFieldType.Integer, record => record.Cycle, group);
        AddField("PreviousEnrollmentId", S["Previous pass"], DataFieldType.Text, record => record.PreviousEnrollmentId, group).Identifier();
        AddField("ProgressPercent", S["Progress (%)"], DataFieldType.Integer, record => record.ProgressPercent, group);
        AddField("Grade", S["Grade (%)"], DataFieldType.Decimal, record => record.Grade, group);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, record => record.Passed, group);
        AddField("Sources", S["Access from"], DataFieldType.Text, record => string.Join(", ", record.Grants.Select(grant => grant.Source).Distinct(StringComparer.Ordinal)), group);
        AddField("CreatedUtc", S["Enrolled"], DataFieldType.DateTime, record => record.CreatedUtc, group);
        AddField("StartedUtc", S["Started"], DataFieldType.DateTime, record => record.StartedUtc, group);
        AddField("DueUtc", S["Due"], DataFieldType.DateTime, record => record.DueUtc, group);
        AddField("AccessEndsUtc", S["Access ends"], DataFieldType.DateTime, record => record.AccessEndsUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, group);
        AddField("LastActivityUtc", S["Last activity"], DataFieldType.DateTime, record => record.LastActivityUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<EnrollmentIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<EnrollmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<EnrollmentIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["CourseId"] = index => index.CourseId,
        };
}
