using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
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
            new DataSetDescriptor(
                ContactCenterReportDataSets.QueueGroups,
                stringLocalizer["Queue groups"],
                stringLocalizer["One row per queue group."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Queue group"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Queue group ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField(nameof(ActivityQueueGroup.Name), S["Name"], DataFieldType.Text, record => record.Name, group);
        AddField(nameof(ActivityQueueGroup.Description), S["Description"], DataFieldType.Text, record => record.Description, group);
        AddField(nameof(ActivityQueueGroup.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(ActivityQueueGroup.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, group);

        CompleteDescription();
    }
}
