using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// One row per timesheet line: the day, pay code, hours and where they came from, with the timesheet's period and
/// status.
/// </summary>
public sealed class TimesheetLinesReportDataSet : WorkforceReportDataSet<Timesheet, TimesheetIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceTimesheetLines";

    /// <summary>
    /// Initializes a new instance of the <see cref="TimesheetLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimesheetLinesReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<TimesheetLinesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Timesheet lines"], stringLocalizer["One row per timesheet line: the day, pay code, hours and where they came from, with the timesheet's period and status."]), session, authorizationService, scopeService, WorkforcePermissions.ApproveTimesheets)
    {
        var S = stringLocalizer;
        var group = S["Timesheet lines"].Value;

        AddField("ItemId", S["Timesheet ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("PeriodStart", S["Period start"], DataFieldType.Text, row => row.Record.PeriodStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Record.Status.ToString(), group);
        AddField("Date", S["Day"], DataFieldType.Text, row => row.Record.Lines.Count > 0 ? row.Record.Lines[0].Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null, group);
        AddField("PayCodeId", S["Pay code ID"], DataFieldType.Text, row => row.Record.Lines.Count > 0 ? row.Record.Lines[0].PayCodeId : null, group);
        AddField("Hours", S["Hours"], DataFieldType.Decimal, row => row.Record.Lines.Count > 0 ? row.Record.Lines[0].Hours : 0, group);
        AddField("Source", S["From"], DataFieldType.Text, row => row.Record.Lines.Count > 0 ? row.Record.Lines[0].Source.ToString() : null, group);
        AddField("ActivityCodeId", S["Activity ID"], DataFieldType.Text, row => row.Record.Lines.Count > 0 ? row.Record.Lines[0].ActivityCodeId : null, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override Expression<Func<TimesheetIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<TimesheetIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TimesheetIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override IEnumerable<WorkforceReportRow<Timesheet>> GetRows(Timesheet document)
    {
        // Each line is its own row: a copy of the timesheet holding just that line.
        foreach (var line in document.Lines)
        {
            yield return Row(new Timesheet
            {
                ItemId = document.ItemId,
                WorkerId = document.WorkerId,
                TeamId = document.TeamId,
                PeriodStart = document.PeriodStart,
                PeriodEnd = document.PeriodEnd,
                Status = document.Status,
                Lines = [line],
            });
        }
    }

    /// <inheritdoc />
    protected override string WorkerOf(Timesheet record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(Timesheet record) => record.TeamId;
}
