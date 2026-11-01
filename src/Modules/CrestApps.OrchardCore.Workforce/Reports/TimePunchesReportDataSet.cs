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
/// One row per punch: when, what, from where, and whether it was at the site, corrected or removed.
/// </summary>
public sealed class TimePunchesReportDataSet : WorkforceReportDataSet<TimePunch, TimePunchIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforcePunches";

    /// <summary>
    /// Initializes a new instance of the <see cref="TimePunchesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimePunchesReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<TimePunchesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Punches"], stringLocalizer["One row per punch: when, what, from where, and whether it was at the site, corrected or removed."]), session, authorizationService, scopeService, WorkforcePermissions.ApproveTimesheets)
    {
        var S = stringLocalizer;
        var group = S["Punches"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("Kind", S["Punch"], DataFieldType.Text, row => row.Record.Kind.ToString(), group);
        AddField("Utc", S["When"], DataFieldType.DateTime, row => row.Record.Utc, group);
        AddField("Source", S["From"], DataFieldType.Text, row => row.Record.Source.ToString(), group);
        AddField("WithinGeofence", S["At the site"], DataFieldType.Boolean, row => row.Record.WithinGeofence, group);
        AddField("ActivityCodeId", S["Activity ID"], DataFieldType.Text, row => row.Record.ActivityCodeId, group);
        AddField("Job", S["Job"], DataFieldType.Text, row => row.Record.Job, group);
        AddField("Corrects", S["Corrects a punch"], DataFieldType.Boolean, row => !string.IsNullOrEmpty(row.Record.ReplacesPunchId) && !row.Record.Removes, group);
        AddField("Removes", S["Removes a punch"], DataFieldType.Boolean, row => row.Record.Removes, group);
        AddField("Reason", S["Reason"], DataFieldType.Text, row => row.Record.Reason, group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<TimePunchIndex, DateTime>> Column)? DateColumn => ("Utc", index => index.Utc);

    /// <inheritdoc />
    protected override Expression<Func<TimePunchIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<TimePunchIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TimePunchIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(TimePunch record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(TimePunch record) => record.TeamId;
}
