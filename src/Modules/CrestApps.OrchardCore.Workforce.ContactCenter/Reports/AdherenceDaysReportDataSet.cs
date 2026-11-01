using System.Globalization;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Reports;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Reports;

/// <summary>
/// One row per agent and day: adherence, conformance and the times they are worked out from.
/// </summary>
public sealed class AdherenceDaysReportDataSet : WorkforceReportDataSet<AdherenceDay, AdherenceDayIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceAdherenceDays";

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceDaysReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceDaysReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<AdherenceDaysReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Adherence days"], stringLocalizer["One row per agent and day: adherence, conformance and the scheduled, worked and excepted hours they come from."]), session, authorizationService, scopeService, WorkforcePermissions.ViewAdherence)
    {
        var S = stringLocalizer;
        var group = S["Adherence days"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("Date", S["Day"], DataFieldType.DateTime, row => row.Record.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), group);
        AddField("Adherence", S["Adherence %"], DataFieldType.Decimal, row => Percent(row.Record.Adherence), group);
        AddField("Conformance", S["Conformance %"], DataFieldType.Decimal, row => Percent(row.Record.Conformance), group);
        AddField("ScheduledHours", S["Scheduled hours"], DataFieldType.Decimal, row => Hours(row.Record.ScheduledSeconds), group);
        AddField("InAdherenceHours", S["Hours in adherence"], DataFieldType.Decimal, row => Hours(row.Record.InAdherenceSeconds), group);
        AddField("ExceptionHours", S["Exception hours"], DataFieldType.Decimal, row => Hours(row.Record.ExceptionSeconds), group);
        AddField("ScheduledWorkHours", S["Scheduled work hours"], DataFieldType.Decimal, row => Hours(row.Record.ScheduledWorkSeconds), group);
        AddField("WorkedHours", S["Worked hours"], DataFieldType.Decimal, row => Hours(row.Record.ActualWorkSeconds), group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override (string Field, Expression<Func<AdherenceDayIndex, DateTime>> Column)? DateColumn => ("Date", index => index.Date);

    /// <inheritdoc />
    protected override Expression<Func<AdherenceDayIndex, string>> TeamColumn => index => index.TeamId;

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<AdherenceDayIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AdherenceDayIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["WorkerId"] = index => index.WorkerId,
        };

    /// <inheritdoc />
    protected override string WorkerOf(AdherenceDay record) => record.WorkerId;

    /// <inheritdoc />
    protected override string TeamOf(AdherenceDay record) => record.TeamId;

    internal static decimal Hours(double seconds) => Math.Round((decimal)(seconds / 3600d), 2);

    internal static decimal? Percent(double? value) => value.HasValue ? Math.Round((decimal)(value.Value * 100d), 1) : null;
}
