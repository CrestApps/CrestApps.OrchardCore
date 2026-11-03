using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Scheduling.Reports.DataSources;

/// <summary>
/// One row per waitlist entry: the appointment type and the time a customer is waiting for, the customer, and where
/// the entry stands. Only principals that may manage appointments read it, as on the waitlist screen.
/// </summary>
internal sealed class WaitlistReportDataSet : IndexedReportDataSet<WaitlistEntry, WaitlistEntryIndex>
{
    /// <summary>
    /// The name of the field that holds when the customer joined the waitlist, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="WaitlistReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session waitlist entries are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WaitlistReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<WaitlistReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                SchedulingReportDataSets.Waitlist,
                stringLocalizer["Waitlist"],
                stringLocalizer["One row per waitlist entry: the appointment type and time a customer is waiting for, the customer, and its status."]),
            session,
            authorizationService,
            SchedulingConstants.CollectionName,
            SchedulingPermissions.ManageAppointments)
    {
        var S = stringLocalizer;
        var entry = S["Waitlist entry"].Value;
        var customer = S["Customer"].Value;
        var dates = S["Dates"].Value;

        AddField(SchedulingReportDataSets.ItemIdField, S["Waitlist entry ID"], DataFieldType.Text, record => record.ItemId, entry).Identifier();
        AddField(nameof(WaitlistEntry.Status), S["Status"], DataFieldType.Text, record => record.Status, entry).Description = S["Waiting, Notified, Booked or Removed."].Value;
        AddField(SchedulingReportDataSets.AppointmentTypeIdField, S["Appointment type ID"], DataFieldType.Text, record => record.AppointmentTypeId, entry)
            .Identifier(SchedulingReportDataSets.Reference(SchedulingReportDataSets.AppointmentTypes));

        AddField(nameof(WaitlistEntry.CustomerName), S["Customer name"], DataFieldType.Text, record => record.CustomerName, customer);
        AddField(nameof(WaitlistEntry.CustomerEmail), S["Customer email"], DataFieldType.Text, record => record.CustomerEmail, customer);
        AddField(nameof(WaitlistEntry.CustomerPhone), S["Customer phone"], DataFieldType.Text, record => record.CustomerPhone, customer);

        AddField(CreatedUtcField, S["Joined"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(WaitlistEntry.FromUtc), S["Wanted from"], DataFieldType.DateTime, record => record.FromUtc, dates);
        AddField(nameof(WaitlistEntry.ToUtc), S["Wanted until"], DataFieldType.DateTime, record => record.ToUtc, dates);
        AddField(nameof(WaitlistEntry.NotifiedUtc), S["Notified"], DataFieldType.DateTime, record => record.NotifiedUtc, dates).Description = S["When the customer was last told a time opened up."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<WaitlistEntryIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<WaitlistEntryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<WaitlistEntryIndex, string>>>(StringComparer.Ordinal)
        {
            [SchedulingReportDataSets.ItemIdField] = index => index.ItemId,
            [SchedulingReportDataSets.AppointmentTypeIdField] = index => index.AppointmentTypeId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<WaitlistEntryIndex> columns)
    {
        columns.Enum(nameof(WaitlistEntry.Status), index => index.Status);
    }
}
