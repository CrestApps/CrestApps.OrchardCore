using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per agent and 15-minute interval: signed-in time, time in each state and the work handled.
/// </summary>
public sealed class AgentIntervalReportDataSet : IndexedReportDataSet<AgentIntervalStatistics, AgentIntervalStatisticsIndex, AgentIntervalStatistic>, IContactCenterReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "AgentIntervals";

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentIntervalReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentIntervalReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AgentIntervalReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DataSetName,
                stringLocalizer["Agent intervals"],
                stringLocalizer["One row per agent and 15-minute interval: signed-in time, time in each state and the work handled."]),
            session,
            authorizationService,
            ContactCenterStorage.CollectionName,
            ContactCenterPermissions.ViewReports)
    {
        var S = stringLocalizer;
        var interval = S["Interval"].Value;
        var states = S["States"].Value;
        var work = S["Work"].Value;

        AddField("AgentId", S["Agent ID"], DataFieldType.Text, row => row.AgentId, interval).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField("UserId", S["User ID"], DataFieldType.Text, row => row.UserId, interval).Identifier(ContactCenterReportDataSets.UserReference());
        AddField("Date", S["Day"], DataFieldType.DateTime, row => IntervalStatisticsConstants.DayStart(row.IntervalStartUtc), interval);
        AddField("IntervalStartUtc", S["Interval start"], DataFieldType.DateTime, row => row.IntervalStartUtc, interval);
        AddField("HalfHourStartUtc", S["Half hour"], DataFieldType.DateTime, row => IntervalStatisticsConstants.IntervalStart(row.IntervalStartUtc, 30), interval);
        AddField("HourStartUtc", S["Hour"], DataFieldType.DateTime, row => IntervalStatisticsConstants.IntervalStart(row.IntervalStartUtc, 60), interval);

        AddField("SignedInSeconds", S["Signed in (seconds)"], DataFieldType.Decimal, row => row.SignedInSeconds, states);

        foreach (var status in new[] { AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentPresenceStatus.Break, AgentPresenceStatus.Away, AgentPresenceStatus.Meeting, AgentPresenceStatus.Training })
        {
            AddField(status + "Seconds", S["{0} (seconds)", status], DataFieldType.Decimal, row => row.SecondsIn(status), states);
        }

        AddField("Handled", S["Handled"], DataFieldType.Integer, row => row.Handled, work);
        AddField("HandleSeconds", S["Handle (seconds)"], DataFieldType.Decimal, row => row.HandleSeconds, work);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<AgentIntervalStatisticsIndex, DateTime>> Column)? DateColumn
        => ("Date", index => index.Date);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AgentIntervalStatisticsIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AgentIntervalStatisticsIndex, string>>>(StringComparer.Ordinal)
        {
            ["AgentId"] = index => index.AgentId,
            ["UserId"] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<AgentIntervalStatistic> GetRows(AgentIntervalStatistics document)
        => document.Intervals;
}
