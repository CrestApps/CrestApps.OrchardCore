using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// One row per person on the staff of a published course, with their role.
/// </summary>
public sealed class LearningCourseStaffReportDataSet : IndexedReportDataSet<ContentItem, CourseStaffIndex, ReportChildRow<ContentItem, CourseStaffMember>>, ILearningReportDataSet
{
    /// <summary>
    /// The stable name of the data set.
    /// </summary>
    public const string DataSetName = "LearningCourseStaff";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCourseStaffReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningCourseStaffReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningCourseStaffReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Course staff"], stringLocalizer["One row per person on the staff of a published course, with their role."]), session, authorizationService, null, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Course staff"].Value;

        AddField("CourseId", S["Course"], DataFieldType.Text, row => row.Parent.ContentItemId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("CourseTitle", S["Course title"], DataFieldType.Text, row => row.Parent.DisplayText, group);
        AddField("UserId", S["Person"], DataFieldType.Text, row => row.Child.UserId, group).Identifier(ReportDataReferences.User());
        AddField("Role", S["Role"], DataFieldType.Text, row => row.Child.Role, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override bool CanAggregate => false;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<CourseStaffIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CourseStaffIndex, string>>>(StringComparer.Ordinal)
        {
            ["CourseId"] = index => index.CourseId,
            ["UserId"] = index => index.UserId,
        };

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<CourseStaffIndex>, IQueryIndex<CourseStaffIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var restrict = await base.RestrictAsync(context, cancellationToken);

        return restrict is null ? null : rows => restrict(rows).Where(index => index.Published);
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportChildRow<ContentItem, CourseStaffMember>> GetRows(ContentItem document)
    {
        var staff = document.Get<CoursePart>(nameof(CoursePart))?.Staff ?? [];

        return ReportChildRow<ContentItem, CourseStaffMember>.For(document, staff.Where(member => !string.IsNullOrEmpty(member?.UserId)));
    }
}
