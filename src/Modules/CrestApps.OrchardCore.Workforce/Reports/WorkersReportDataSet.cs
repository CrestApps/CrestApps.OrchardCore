using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using ISession = YesSql.ISession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// One row per worker: their team, employment, status, hours and dates.
/// </summary>
public sealed class WorkersReportDataSet : WorkforceReportDataSet<StaffMember, StaffMemberIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceWorkers";

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkersReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<WorkersReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Workers"], stringLocalizer["One row per worker: their main team, employment, status, contracted hours, and when they were hired and left."]), session, authorizationService, scopeService, WorkforcePermissions.ViewWorkers, StaffConstants.CollectionName)
    {
        var S = stringLocalizer;
        var group = S["Workers"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.Record.DisplayName, group);
        AddField("EmployeeNumber", S["Employee number"], DataFieldType.Text, row => row.Record.EmployeeNumber, group);
        AddField("JobTitle", S["Job title"], DataFieldType.Text, row => row.Record.JobTitle, group);
        AddField("TeamName", S["Main team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("Teams", S["Teams"], DataFieldType.Integer, row => row.Record.TeamMemberships.Count, group);
        AddField("EmploymentType", S["Employment"], DataFieldType.Text, row => row.Record.EmploymentType.ToString(), group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Record.Status.ToString(), group);
        AddField("ContractedHoursPerWeek", S["Contracted hours a week"], DataFieldType.Decimal, row => row.Record.ContractedHoursPerWeek, group);
        AddField("HireDate", S["Hired"], DataFieldType.Date, row => row.Record.HireDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("TerminationDate", S["Left"], DataFieldType.Date, row => row.Record.TerminationDate?.ToDateTime(TimeOnly.MinValue), group);
        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, row => row.Record.CreatedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override Expression<Func<StaffMemberIndex, string>> TeamColumn => index => index.PrimaryTeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<StaffMemberIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StaffMemberIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(StaffMember record) => record.ItemId;

    /// <inheritdoc />
    protected override string TeamOf(StaffMember record) => record.PrimaryTeamId;
}

/// <summary>
/// One row per change to a published schedule: what changed, for whom, how late, and whether the worker asked.
/// </summary>
public sealed class ScheduleChangesReportDataSet : WorkforceReportDataSet<ScheduleChange, ScheduleChangeIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceScheduleChanges";

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleChangesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ScheduleChangesReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<ScheduleChangesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Schedule changes"], stringLocalizer["One row per change to a published schedule: added, moved, reassigned, swapped or removed, for whom, how many hours before the shift, and whether the worker asked."]), session, authorizationService, scopeService, WorkforcePermissions.ManageSchedules)
    {
        var S = stringLocalizer;
        var group = S["Schedule changes"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("Kind", S["Change"], DataFieldType.Text, row => row.Record.Kind.ToString(), group);
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("PeriodId", S["Schedule ID"], DataFieldType.Text, row => row.Record.PeriodId, group);
        AddField("BeforeStartUtc", S["Was at"], DataFieldType.DateTime, row => row.Record.BeforeStartUtc, group);
        AddField("AfterStartUtc", S["Now at"], DataFieldType.DateTime, row => row.Record.AfterStartUtc, group);
        AddField("HoursNotice", S["Hours of notice"], DataFieldType.Decimal, row => HoursNotice(row.Record), group);
        AddField("WorkerAsked", S["The worker asked"], DataFieldType.Boolean, row => row.Record.InitiatedBy == ScheduleChangeInitiator.Worker, group);
        AddField("Source", S["Made by"], DataFieldType.Text, row => row.Record.Source, group);
        AddField("Reason", S["Reason"], DataFieldType.Text, row => row.Record.Reason, group);
        AddField("ChangedUtc", S["Changed"], DataFieldType.DateTime, row => row.Record.ChangedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<ScheduleChangeIndex, DateTime>> Column)? DateColumn => ("ChangedUtc", index => index.ChangedUtc);

    /// <inheritdoc />
    protected override Expression<Func<ScheduleChangeIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<ScheduleChangeIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ScheduleChangeIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
            ["PeriodId"] = index => index.PeriodId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(ScheduleChange record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(ScheduleChange record) => record.TeamId;

    // How long before the shift (its earlier start, or its new one when added) the change was made.
    private static decimal? HoursNotice(ScheduleChange change)
        => (change.BeforeStartUtc ?? change.AfterStartUtc) is { } start ? Math.Round((decimal)(start - change.ChangedUtc).TotalHours, 1) : null;
}
