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
/// One row per segment of an agent's scheduled day: the activity, the agent's state and whether it was in adherence.
/// </summary>
public sealed class AdherenceSegmentsReportDataSet : WorkforceReportDataSet<AdherenceDay, AdherenceDayIndex>
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceAdherenceSegments";

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceSegmentsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceSegmentsReportDataSet(ISession session, IAuthorizationService authorizationService, IWorkforceScopeService scopeService, IStringLocalizer<AdherenceSegmentsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Adherence segments"], stringLocalizer["One row per stretch of an agent's scheduled day: the scheduled activity, the agent's state and whether it was in adherence."]), session, authorizationService, scopeService, WorkforcePermissions.ViewAdherence)
    {
        var S = stringLocalizer;
        var group = S["Adherence segments"].Value;

        AddField("ItemId", S["Day ID"], DataFieldType.Text, row => row.Record.ItemId, group).Identifier();
        AddField("WorkerId", S["Worker ID"], DataFieldType.Text, row => row.Record.WorkerId, group);
        AddField("WorkerName", S["Worker"], DataFieldType.Text, row => row.WorkerName, group);
        AddField("TeamName", S["Team"], DataFieldType.Text, row => row.TeamName, group);
        AddField("Date", S["Day"], DataFieldType.Date, row => row.Record.Date.ToDateTime(TimeOnly.MinValue), group);
        AddField("StartUtc", S["Start"], DataFieldType.DateTime, row => Segment(row)?.StartUtc, group);
        AddField("EndUtc", S["End"], DataFieldType.DateTime, row => Segment(row)?.EndUtc, group);
        AddField("Activity", S["Scheduled activity"], DataFieldType.Text, row => Segment(row)?.Kind.ToString(), group);
        AddField("State", S["Agent state"], DataFieldType.Text, row => Segment(row)?.Status.ToString(), group);
        AddField("Reason", S["State reason"], DataFieldType.Text, row => Segment(row)?.Reason, group);
        AddField("InAdherence", S["In adherence"], DataFieldType.Boolean, row => Segment(row)?.InAdherence, group);
        AddField("Excepted", S["Exception"], DataFieldType.Boolean, row => Segment(row)?.Excepted, group);
        AddField("Minutes", S["Minutes"], DataFieldType.Decimal, row => Segment(row) is { } segment ? Math.Round((decimal)(segment.Seconds / 60d), 1) : 0m, group);

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

    /// <inheritdoc />
    protected override IEnumerable<WorkforceReportRow<AdherenceDay>> GetRows(AdherenceDay document)
    {
        // Each segment is its own row: a copy of the day holding just that segment.
        foreach (var segment in document.Segments)
        {
            yield return Row(new AdherenceDay
            {
                ItemId = document.ItemId,
                WorkerId = document.WorkerId,
                TeamId = document.TeamId,
                Date = document.Date,
                Segments = [segment],
            });
        }
    }

    private static AdherenceSegment Segment(WorkforceReportRow<AdherenceDay> row)
        => row.Record.Segments.Count > 0 ? row.Record.Segments[0] : null;
}
