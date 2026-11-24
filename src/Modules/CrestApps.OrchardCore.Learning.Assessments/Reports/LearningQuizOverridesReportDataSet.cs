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
/// One row per quiz override: extra time or attempts, or another window, given to one learner, with who and why.
/// </summary>
public sealed class LearningQuizOverridesReportDataSet : IndexedReportDataSet<AssessmentOverride, AssessmentOverrideIndex>, IAssessmentsReportDataSet
{
    /// <summary>
    /// The name of the data set.
    /// </summary>
    public const string DataSetName = "LearningQuizOverrides";

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningQuizOverridesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningQuizOverridesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningQuizOverridesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Quiz overrides"], stringLocalizer["One row per quiz override given to a learner: extra time, extra attempts or another window, with who gave it and why."]), session, authorizationService, LearningConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Override"].Value;

        AddField("OverrideId", S["Override ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("UserId", S["Learner"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("QuizId", S["Quiz"], DataFieldType.Text, record => record.QuizId, group).Identifier(ReportDataReferences.ContentItem(AssessmentsConstants.ContentTypes.Quiz));
        AddField("ExtraMinutes", S["Extra minutes"], DataFieldType.Integer, record => record.ExtraMinutes, group);
        AddField("ExtraAttempts", S["Extra attempts"], DataFieldType.Integer, record => record.ExtraAttempts, group);
        AddField("OpensUtc", S["Opens"], DataFieldType.DateTime, record => record.OpensUtc, group);
        AddField("ClosesUtc", S["Closes"], DataFieldType.DateTime, record => record.ClosesUtc, group);
        AddField("Reason", S["Reason"], DataFieldType.Text, record => record.Reason, group);
        AddField("Actor", S["Given by"], DataFieldType.Text, record => record.Actor, group).Identifier(ReportDataReferences.User());
        AddField("CreatedUtc", S["Given"], DataFieldType.DateTime, record => record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AssessmentOverrideIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AssessmentOverrideIndex, string>>>(StringComparer.Ordinal)
        {
            ["OverrideId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
            ["QuizId"] = index => index.QuizId,
        };
}
