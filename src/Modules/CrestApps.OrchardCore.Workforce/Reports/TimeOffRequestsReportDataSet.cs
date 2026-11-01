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
/// One row per time-off request: its dates, amount, status and when it was asked for and decided.
/// </summary>
public sealed class TimeOffRequestsReportDataSet : WorkforceReportDataSet<TimeOffRequest, TimeOffRequestIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceTimeOffRequests";

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffRequestsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffRequestsReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<TimeOffRequestsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Time-off requests"], stringLocalizer["One row per time-off request: its dates, amount, status and when it was asked for and decided."]), session, authorizationService, scopeService, WorkforcePermissions.ApproveTimeOff)
    {
        var S = stringLocalizer;
        var group = S["Time-off requests"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("TypeId", S["Type ID"], DataFieldType.Text, row => row.Record.TypeId, group);
        AddField("StartDate", S["First day"], DataFieldType.Text, row => row.Record.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), group);
        AddField("EndDate", S["Last day"], DataFieldType.Text, row => row.Record.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), group);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, row => row.Record.Amount, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Record.Status.ToString(), group);
        AddField("SubmittedUtc", S["Asked for"], DataFieldType.DateTime, row => row.Record.SubmittedUtc, group);
        AddField("DecidedUtc", S["Decided"], DataFieldType.DateTime, row => row.Record.DecidedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<TimeOffRequestIndex, DateTime>> Column)? DateColumn => ("SubmittedUtc", index => index.SubmittedUtc);

    /// <inheritdoc />
    protected override Expression<Func<TimeOffRequestIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<TimeOffRequestIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TimeOffRequestIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(TimeOffRequest record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(TimeOffRequest record) => record.TeamId;
}
