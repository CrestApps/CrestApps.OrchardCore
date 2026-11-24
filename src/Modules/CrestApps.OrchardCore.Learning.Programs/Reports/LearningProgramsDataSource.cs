using System.Linq.Expressions;
using CrestApps.OrchardCore.Learning.Programs.Core.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using ISession = YesSql.ISession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Programs.Reports;

/// <summary>
/// The Learning programs data source of Data Pipelines and the Report Builder: path progress, and with required
/// learning on, the rules and each person's state.
/// </summary>
public sealed class LearningProgramsDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    public LearningProgramsDataSource(LearningPathProgressReportDataSet paths, IServiceProvider services)
    {
        _dataSets = services.GetService<RequiredLearningStateReportDataSet>() is { } states
            ? [paths, states, services.GetRequiredService<LearningRequirementReportDataSet>()]
            : [paths];
    }

    public static string Name => ProgramsConstants.DataSourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningProgramsDataSource>("Learning programs");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningProgramsDataSource>("Learning path progress, required learning rules and where each person stands under them.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per person on a learning path.
/// </summary>
public sealed class LearningPathProgressReportDataSet : IndexedReportDataSet<LearningPathProgress, LearningPathProgressIndex>
{
    public const string DataSetName = "LearningPathProgress";

    public LearningPathProgressReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningPathProgressReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Path progress"], stringLocalizer["One row per person on a learning path: the stage open to them, the courses they completed and when they completed the path."]), session, authorizationService, ProgramsConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Path progress"].Value;

        AddField("ProgressId", S["Progress ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("PathId", S["Path"], DataFieldType.Text, record => record.PathId, group).Identifier(ReportDataReferences.ContentItem(ProgramsConstants.LearningPathContentType));
        AddField("UserId", S["Person"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("State", S["State"], DataFieldType.Text, record => record.State.ToString(), group);
        AddField("Stage", S["Open stage"], DataFieldType.Integer, record => record.OpenedStage + 1, group);
        AddField("CoursesCompleted", S["Courses completed"], DataFieldType.Integer, record => record.CompletedCourseIds.Count, group);
        AddField("Source", S["Started by"], DataFieldType.Text, record => record.Source, group);
        AddField("StartedUtc", S["Started"], DataFieldType.DateTime, record => record.StartedUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, group);

        CompleteDescription();
    }

    protected override IReadOnlyDictionary<string, Expression<Func<LearningPathProgressIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LearningPathProgressIndex, string>>>(StringComparer.Ordinal)
        {
            ["ProgressId"] = index => index.ItemId,
            ["PathId"] = index => index.PathId,
            ["UserId"] = index => index.UserId,
        };
}

/// <summary>
/// One row per required learning rule.
/// </summary>
public sealed class LearningRequirementReportDataSet : IndexedReportDataSet<LearningRequirement, LearningRequirementIndex>
{
    public const string DataSetName = "LearningRequirements";

    public LearningRequirementReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<LearningRequirementReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Required learning"], stringLocalizer["One row per required learning rule: what it requires, of whom and when it is due."]), session, authorizationService, ProgramsConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["Required learning"].Value;

        AddField("RequirementId", S["Rule ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, record => record.Name, group);
        AddField("Enabled", S["On"], DataFieldType.Boolean, record => record.Enabled, group);
        AddField("TargetKind", S["Kind of target"], DataFieldType.Text, record => record.TargetKind, group);
        AddField("TargetId", S["Target"], DataFieldType.Text, record => record.TargetId, group).Identifier();
        AddField("Audience", S["Audience"], DataFieldType.Text, record => record.AudienceName, group);
        AddField("NamedPeople", S["Named people"], DataFieldType.Integer, record => record.UserIds.Count, group);
        AddField("DueRule", S["Due rule"], DataFieldType.Text, record => record.DueRule.ToString(), group);
        AddField("DueDate", S["Due date"], DataFieldType.Date, record => record.DueDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("DueDays", S["Due days"], DataFieldType.Integer, record => record.DueDays, group);
        AddField("LastRunUtc", S["Last run"], DataFieldType.DateTime, record => record.LastRunUtc, group);
        AddField("Covered", S["People covered"], DataFieldType.Integer, record => record.LastRunCovered, group);

        CompleteDescription();
    }

    protected override IReadOnlyDictionary<string, Expression<Func<LearningRequirementIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<LearningRequirementIndex, string>>>(StringComparer.Ordinal)
        {
            ["RequirementId"] = index => index.ItemId,
            ["TargetId"] = index => index.TargetId,
        };
}

/// <summary>
/// One row per person under a required learning rule.
/// </summary>
public sealed class RequiredLearningStateReportDataSet : IndexedReportDataSet<RequiredLearningAssignment, RequiredLearningAssignmentIndex>
{
    public const string DataSetName = "RequiredLearningStates";

    public RequiredLearningStateReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<RequiredLearningStateReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["State per person"], stringLocalizer["One row per person under a required learning rule: the state, the due date, completion and exemption."]), session, authorizationService, ProgramsConstants.CollectionName, LearningPermissions.ViewLearningReports)
    {
        var S = stringLocalizer;
        var group = S["State per person"].Value;

        AddField("AssignmentId", S["State ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField("RequirementId", S["Rule"], DataFieldType.Text, record => record.RequirementId, group).Identifier();
        AddField("UserId", S["Person"], DataFieldType.Text, record => record.UserId, group).Identifier(ReportDataReferences.User());
        AddField("State", S["State"], DataFieldType.Text, record => record.State.ToString(), group);
        AddField("RequiredUtc", S["Required"], DataFieldType.DateTime, record => record.RequiredUtc, group);
        AddField("DueUtc", S["Due"], DataFieldType.DateTime, record => record.DueUtc, group);
        AddField("CompletedUtc", S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, group);
        AddField("Escalated", S["Manager told"], DataFieldType.Boolean, record => record.EscalatedUtc is not null, group);
        AddField("ExemptionReason", S["Exemption reason"], DataFieldType.Text, record => record.Exemption?.Reason, group);
        AddField("ExemptedBy", S["Exempted by"], DataFieldType.Text, record => record.Exemption?.GrantedBy, group);

        CompleteDescription();
    }

    protected override IReadOnlyDictionary<string, Expression<Func<RequiredLearningAssignmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<RequiredLearningAssignmentIndex, string>>>(StringComparer.Ordinal)
        {
            ["AssignmentId"] = index => index.ItemId,
            ["RequirementId"] = index => index.RequirementId,
            ["UserId"] = index => index.UserId,
        };
}
