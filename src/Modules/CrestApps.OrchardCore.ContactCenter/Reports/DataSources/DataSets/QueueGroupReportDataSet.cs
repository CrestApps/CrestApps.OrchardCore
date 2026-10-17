using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per queue group.
/// </summary>
public sealed class QueueGroupReportDataSet : ContactCenterReportDataSet<ActivityQueueGroup, ActivityQueueGroupIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueGroupReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session queue groups are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueGroupReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<QueueGroupReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.QueueGroups,
                stringLocalizer["Queue groups"],
                stringLocalizer["One row per queue group."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Queue group"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Queue group ID"], ReportDataType.Text, record => record.ItemId, group, isIdentifier: true);
        AddField(nameof(ActivityQueueGroup.Name), S["Name"], ReportDataType.Text, record => record.Name, group);
        AddField(nameof(ActivityQueueGroup.Description), S["Description"], ReportDataType.Text, record => record.Description, group);
        AddField(nameof(ActivityQueueGroup.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(ActivityQueueGroup.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, group);
    }
}
