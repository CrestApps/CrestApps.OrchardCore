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
/// One row per course item a learner opened: its state, times, time spent and score.
/// </summary>
public sealed class LearningItemProgressReportDataSet : IndexedReportDataSet<EnrollmentProgress, EnrollmentProgressIndex, ReportChildRow<EnrollmentProgress, CourseItemProgress>>, ILearningReportDataSet
{
    /// <summary>
    /// The stable name of the data set.
    /// </summary>
    public const string DataSetName = "LearningItemProgress";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningItemProgressReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningItemProgressReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningItemProgressReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Course item progress"], stringLocalizer["One row per course item a learner opened: the state, when it was opened and completed, the time spent and the score."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Progress"].Value;

        AddField("EnrollmentId", S["Enrollment ID"], DataFieldType.Text, row => row.Parent.EnrollmentId, group).Identifier(new DataFieldReference(LearningConstants.DataSourceName, LearningEnrollmentsReportDataSet.DataSetName, "ItemId"));
        AddField("UserId", S["Learner"], DataFieldType.Text, row => row.Parent.UserId, group).Identifier(ReportDataReferences.User());
        AddField("CourseId", S["Course"], DataFieldType.Text, row => row.Parent.CourseId, group).Identifier(ReportDataReferences.ContentItem(LearningConstants.ContentTypes.Course));
        AddField("ContentItemId", S["Course item"], DataFieldType.Text, row => row.Child.ContentItemId, group).Identifier();
        AddField("State", S["State"], DataFieldType.Text, row => row.Child.State.ToString(), group);
        AddField("FirstAccessUtc", S["First opened"], DataFieldType.DateTime, row => row.Child.FirstAccessUtc, group);
        AddField("LastAccessUtc", S["Last opened"], DataFieldType.DateTime, row => row.Child.LastAccessUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, row => row.Child.CompletedUtc, group);
        AddField("SecondsSpent", S["Time spent (seconds)"], DataFieldType.Integer, row => row.Child.SecondsSpent, group);
        AddField("PercentViewed", S["Watched (%)"], DataFieldType.Integer, row => row.Child.PercentViewed, group);
        AddField("Score", S["Score (%)"], DataFieldType.Decimal, row => row.Child.Score, group);
        AddField("Passed", S["Passed"], DataFieldType.Boolean, row => row.Child.Passed, group);
        AddField("Attempts", S["Attempts"], DataFieldType.Integer, row => row.Child.Attempts, group);
        AddField("SelfReported", S["Reported by the browser"], DataFieldType.Boolean, row => row.Child.SelfReported, group);
        AddField("CompletedBy", S["Completed by hand by"], DataFieldType.Text, row => row.Child.CompletedBy, group).Identifier(ReportDataReferences.User());

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<EnrollmentProgressIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<EnrollmentProgressIndex, string>>>(StringComparer.Ordinal)
        {
            ["EnrollmentId"] = index => index.EnrollmentId,
            ["UserId"] = index => index.UserId,
            ["CourseId"] = index => index.CourseId,
        };

    /// <inheritdoc />
    protected override IEnumerable<ReportChildRow<EnrollmentProgress, CourseItemProgress>> GetRows(EnrollmentProgress document)
        => ReportChildRow<EnrollmentProgress, CourseItemProgress>.For(document, document.Items);
}
