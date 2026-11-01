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
/// One row per appointment: its type, the resources it books, the customer, when it is, where it stands, and how far
/// ahead it was booked. The customer's notes, the reason it was canceled, and the link that lets the customer change it
/// are never exposed.
/// </summary>
internal sealed class AppointmentsReportDataSet : IndexedReportDataSet<Appointment, AppointmentIndex>
{
    /// <summary>
    /// The name of the field that holds when the appointment starts, the data set's main date.
    /// </summary>
    public const string StartUtcField = "StartUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session appointments are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AppointmentsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                SchedulingReportDataSets.Appointments,
                stringLocalizer["Appointments"],
                stringLocalizer["One row per appointment: its type, resources, customer, when it is, its status, and how far ahead it was booked."]),
            session,
            authorizationService,
            SchedulingConstants.CollectionName,
            SchedulingPermissions.ViewAppointments)
    {
        var S = stringLocalizer;
        var appointment = S["Appointment"].Value;
        var customer = S["Customer"].Value;
        var dates = S["Dates"].Value;

        AddField(SchedulingReportDataSets.ItemIdField, S["Appointment ID"], DataFieldType.Text, record => record.ItemId, appointment).Identifier();
        AddField(nameof(Appointment.Status), S["Status"], DataFieldType.Text, record => record.Status, appointment).Description = S["Held, Requested, Confirmed, Completed, NoShow, Cancelled or Expired."].Value;
        AddField("IsCancelled", S["Cancelled"], DataFieldType.Boolean, record => record.Status == AppointmentStatus.Cancelled, appointment);
        AddField("IsNoShow", S["No-show"], DataFieldType.Boolean, record => record.Status == AppointmentStatus.NoShow, appointment);
        AddField(SchedulingReportDataSets.AppointmentTypeIdField, S["Appointment type ID"], DataFieldType.Text, record => record.AppointmentTypeId, appointment)
            .Identifier(SchedulingReportDataSets.Reference(SchedulingReportDataSets.AppointmentTypes));
        AddField(nameof(Appointment.ResourceIds), S["Resource IDs"], DataFieldType.Text, record => SchedulingReportDataSets.Join(record.ResourceIds), appointment).Description = S["The staff, rooms and equipment it books, separated by commas. Join the appointment resources data set for one row per resource."].Value;
        AddField("ResourceCount", S["Resources"], DataFieldType.Integer, record => record.ResourceIds?.Count ?? 0, appointment);
        AddField(nameof(Appointment.Source), S["Booked from"], DataFieldType.Text, record => record.Source, appointment).Description = S["Where it was booked: Admin, SelfService, Api, AI or Workflow."].Value;
        AddField(nameof(Appointment.CalendarEventId), S["Calendar event ID"], DataFieldType.Text, record => record.CalendarEventId, appointment)
            .Identifier(SchedulingReportDataSets.CalendarReference("CalendarEvents"));

        AddField(nameof(Appointment.CustomerName), S["Customer name"], DataFieldType.Text, record => record.CustomerName, customer);
        AddField(nameof(Appointment.CustomerEmail), S["Customer email"], DataFieldType.Text, record => record.CustomerEmail, customer);
        AddField(nameof(Appointment.CustomerPhone), S["Customer phone"], DataFieldType.Text, record => record.CustomerPhone, customer);
        AddField(nameof(Appointment.UserId), S["Customer user ID"], DataFieldType.Text, record => record.UserId, customer).Identifier(ReportDataReferences.User());

        AddField(StartUtcField, S["Starts"], DataFieldType.DateTime, record => record.StartUtc, dates);
        AddField(nameof(Appointment.EndUtc), S["Ends"], DataFieldType.DateTime, record => record.EndUtc, dates);
        AddField("DurationMinutes", S["Duration (minutes)"], DataFieldType.Integer, record => SchedulingReportDataSets.Minutes(record.StartUtc, record.EndUtc), dates);
        AddField(nameof(Appointment.TimeZoneId), S["Time zone"], DataFieldType.Text, record => record.TimeZoneId, dates);
        AddField(nameof(Appointment.CreatedUtc), S["Booked"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("LeadTimeMinutes", S["Lead time (minutes)"], DataFieldType.Integer, record => SchedulingReportDataSets.Minutes(record.CreatedUtc, record.StartUtc), dates).Description = S["How long before it starts it was booked."].Value;
        AddField(nameof(Appointment.HoldExpiresUtc), S["Hold ends"], DataFieldType.DateTime, record => record.HoldExpiresUtc, dates);
        AddField(nameof(Appointment.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates).Description = S["When it was last changed, such as moved, confirmed or canceled."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<AppointmentIndex, DateTime>> Column)? DateColumn
        => (StartUtcField, index => index.StartUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AppointmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AppointmentIndex, string>>>(StringComparer.Ordinal)
        {
            [SchedulingReportDataSets.ItemIdField] = index => index.ItemId,
            [SchedulingReportDataSets.AppointmentTypeIdField] = index => index.AppointmentTypeId,
            [nameof(Appointment.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<AppointmentIndex> columns)
    {
        columns.Enum(nameof(Appointment.Status), index => index.Status);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the customer's name shortened and their email address in lower case, so those columns are
        // not always the fields' values.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Appointment.CustomerName));
        columns.Remove(nameof(Appointment.CustomerEmail));
        columns.Remove(nameof(Appointment.CustomerPhone));

        return columns;
    }
}
