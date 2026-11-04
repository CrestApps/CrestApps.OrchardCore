using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per queue, channel and 15-minute interval: volume, service level, waits and handle time. The half-hour
/// and hour fields group the rows into longer intervals.
/// </summary>
public sealed class QueueIntervalReportDataSet : IndexedReportDataSet<QueueIntervalStatistics, QueueIntervalStatisticsIndex, QueueIntervalStatistic>, IContactCenterReportDataSet
{
    /// <summary>
    /// The data set's technical name.
    /// </summary>
    public const string DataSetName = "QueueIntervals";

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueIntervalReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueIntervalReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<QueueIntervalReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                DataSetName,
                stringLocalizer["Queue intervals"],
                stringLocalizer["One row per queue, channel and 15-minute interval: volume, service level, waits and handle time."]),
            session,
            authorizationService,
            ContactCenterStorage.CollectionName,
            ContactCenterPermissions.ViewReports)
    {
        var S = stringLocalizer;
        var interval = S["Interval"].Value;
        var volume = S["Volume"].Value;
        var time = S["Time"].Value;

        AddField("QueueId", S["Queue ID"], DataFieldType.Text, row => row.QueueId, interval).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField("Channel", S["Channel"], DataFieldType.Text, row => row.Channel.ToString(), interval);
        AddField("Date", S["Day"], DataFieldType.DateTime, row => IntervalStatisticsConstants.DayStart(row.IntervalStartUtc), interval);
        AddField("IntervalStartUtc", S["Interval start"], DataFieldType.DateTime, row => row.IntervalStartUtc, interval);
        AddField("HalfHourStartUtc", S["Half hour"], DataFieldType.DateTime, row => IntervalStatisticsConstants.IntervalStart(row.IntervalStartUtc, 30), interval);
        AddField("HourStartUtc", S["Hour"], DataFieldType.DateTime, row => IntervalStatisticsConstants.IntervalStart(row.IntervalStartUtc, 60), interval);

        AddField("Offered", S["Offered"], DataFieldType.Integer, row => row.Offered, volume);
        AddField("Outbound", S["Outbound"], DataFieldType.Integer, row => row.Outbound, volume);
        AddField("Answered", S["Answered"], DataFieldType.Integer, row => row.Answered, volume);
        AddField("Handled", S["Handled"], DataFieldType.Integer, row => row.Handled, volume);
        AddField("Abandoned", S["Abandoned"], DataFieldType.Integer, row => row.Abandoned, volume);
        AddField("ShortAbandoned", S["Short abandons"], DataFieldType.Integer, row => row.ShortAbandoned, volume);
        AddField("Voicemail", S["Voicemail"], DataFieldType.Integer, row => row.Voicemail, volume);
        AddField("CallbackRequested", S["Callbacks"], DataFieldType.Integer, row => row.CallbackRequested, volume);
        AddField("Transferred", S["Transferred"], DataFieldType.Integer, row => row.Transferred, volume);
        AddField("ServiceLevelOffered", S["Service level offered"], DataFieldType.Integer, row => row.ServiceLevelOffered, volume);
        AddField("AnsweredWithinServiceLevel", S["Answered within service level"], DataFieldType.Integer, row => row.AnsweredWithinServiceLevel, volume);

        AddField("ServiceLevelThresholdSeconds", S["Service level threshold (seconds)"], DataFieldType.Integer, row => row.ServiceLevelThresholdSeconds, time);
        AddField("AnswerWaitSeconds", S["Answer wait (seconds)"], DataFieldType.Decimal, row => row.AnswerWaitSeconds, time);
        AddField("MaxAnswerWaitSeconds", S["Longest answer wait (seconds)"], DataFieldType.Decimal, row => row.MaxAnswerWaitSeconds, time);
        AddField("TalkSeconds", S["Talk (seconds)"], DataFieldType.Decimal, row => row.TalkSeconds, time);
        AddField("WrapUpSeconds", S["Wrap-up (seconds)"], DataFieldType.Decimal, row => row.WrapUpSeconds, time);
        AddField("HandleSeconds", S["Handle (seconds)"], DataFieldType.Decimal, row => row.HandleSeconds, time);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<QueueIntervalStatisticsIndex, DateTime>> Column)? DateColumn
        => ("Date", index => index.Date);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<QueueIntervalStatisticsIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<QueueIntervalStatisticsIndex, string>>>(StringComparer.Ordinal)
        {
            ["QueueId"] = index => index.QueueId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<QueueIntervalStatistic> GetRows(QueueIntervalStatistics document)
        => document.Intervals;
}
