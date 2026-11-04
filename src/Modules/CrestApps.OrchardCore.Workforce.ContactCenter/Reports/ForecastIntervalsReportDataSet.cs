using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Reports;

/// <summary>
/// One row per forecast and 15-minute interval: the contacts expected, the handle time and the agents needed.
/// </summary>
public sealed class ForecastIntervalsReportDataSet : IndexedReportDataSet<Forecast, ForecastIndex, ForecastIntervalReportRow>, IWorkforceReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "WorkforceForecastIntervals";

    private Dictionary<string, string> _groups = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastIntervalsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastIntervalsReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<ForecastIntervalsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(DataSetName, stringLocalizer["Forecast intervals"], stringLocalizer["One row per forecast and 15-minute interval: the contacts expected, the handle time, and the agents needed to answer and to schedule."]), session, authorizationService, WorkforceConstants.CollectionName, WorkforcePermissions.ManageForecasts)
    {
        var S = stringLocalizer;
        var group = S["Forecast intervals"].Value;

        AddField("ForecastId", S["Forecast ID"], DataFieldType.Text, row => row.Forecast.ItemId, group).Identifier();
        AddField("PlanningGroupId", S["Planning group ID"], DataFieldType.Text, row => row.Forecast.PlanningGroupId, group);
        AddField("PlanningGroupName", S["Planning group"], DataFieldType.Text, row => row.PlanningGroupName, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Forecast.Status.ToString(), group);
        AddField("Model", S["Model"], DataFieldType.Text, row => row.Forecast.Model.ToString(), group);
        AddField("Date", S["Day"], DataFieldType.Date, row => row.Interval.Date.ToDateTime(TimeOnly.MinValue), group);
        AddField("IntervalStartUtc", S["Interval start"], DataFieldType.DateTime, row => row.Interval.StartUtc, group);
        AddField("Volume", S["Contacts"], DataFieldType.Decimal, row => Math.Round((decimal)row.Interval.Volume, 2), group);
        AddField("AverageHandleSeconds", S["Average handle (seconds)"], DataFieldType.Decimal, row => Math.Round((decimal)row.Interval.AverageHandleSeconds, 1), group);
        AddField("RequiredAgents", S["Agents needed"], DataFieldType.Integer, row => row.Interval.RequiredAgents, group);
        AddField("RequiredScheduled", S["Agents to schedule"], DataFieldType.Integer, row => row.Interval.RequiredScheduled, group);
        AddField("ServiceLevel", S["Service level %"], DataFieldType.Decimal, row => Math.Round((decimal)(row.Interval.ServiceLevel * 100d), 1), group);
        AddField("AverageSpeedOfAnswerSeconds", S["Average speed of answer (seconds)"], DataFieldType.Decimal, row => Math.Round((decimal)row.Interval.AverageSpeedOfAnswerSeconds, 1), group);
        AddField("Occupancy", S["Occupancy %"], DataFieldType.Decimal, row => Math.Round((decimal)(row.Interval.Occupancy * 100d), 1), group);

        CompleteDescription();
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, Expression<Func<ForecastIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ForecastIndex, string>>>(StringComparer.Ordinal)
        {
            ["ForecastId"] = index => index.ItemId,
            ["PlanningGroupId"] = index => index.PlanningGroupId,
        };

    /// <inheritdoc />
    protected override IEnumerable<ForecastIntervalReportRow> GetRows(Forecast document)
    {
        var name = _groups.GetValueOrDefault(document.PlanningGroupId ?? string.Empty);

        return document.Intervals.Select(interval => new ForecastIntervalReportRow { Forecast = document, Interval = interval, PlanningGroupName = name });
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<ForecastIndex>, IQueryIndex<ForecastIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        _groups = (await Session.QueryIndex<PlanningGroupIndex>(collection: WorkforceConstants.CollectionName).ListAsync(cancellationToken))
            .GroupBy(index => index.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);

        return rows => rows;
    }
}

/// <summary>
/// A row of the forecast intervals data set.
/// </summary>
public sealed class ForecastIntervalReportRow
{
    /// <summary>
    /// Gets or sets the forecast.
    /// </summary>
    public Forecast Forecast { get; set; }

    /// <summary>
    /// Gets or sets the interval.
    /// </summary>
    public ForecastInterval Interval { get; set; }

    /// <summary>
    /// Gets or sets the planning group's name.
    /// </summary>
    public string PlanningGroupName { get; set; }
}
