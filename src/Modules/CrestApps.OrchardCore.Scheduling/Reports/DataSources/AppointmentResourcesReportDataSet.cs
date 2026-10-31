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
/// One row per resource an appointment books, such as each staff member and room of it, so appointments can be counted
/// and their time summed by resource. An appointment that books no resource gives no row.
/// </summary>
internal sealed class AppointmentResourcesReportDataSet : IndexedReportDataSet<Appointment, AppointmentIndex, ReportChildRow<Appointment, string>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentResourcesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session appointments are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentResourcesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AppointmentResourcesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                SchedulingReportDataSets.AppointmentResources,
                stringLocalizer["Appointment resources"],
                stringLocalizer["One row per staff member, room or piece of equipment an appointment books, with the appointment's type, status and time."]),
            session,
            authorizationService,
            SchedulingConstants.CollectionName,
            SchedulingPermissions.ViewAppointments)
    {
        var S = stringLocalizer;
        var booking = S["Booking"].Value;
        var appointment = S["Appointment"].Value;

        AddField(SchedulingReportDataSets.AppointmentIdField, S["Appointment ID"], DataFieldType.Text, row => row.Parent.ItemId, booking)
            .Identifier(SchedulingReportDataSets.Reference(SchedulingReportDataSets.Appointments));
        AddField(SchedulingReportDataSets.ResourceIdField, S["Resource ID"], DataFieldType.Text, row => row.Child, booking)
            .Identifier(SchedulingReportDataSets.Reference(SchedulingReportDataSets.Resources));
        AddField("Position", S["Position"], DataFieldType.Integer, row => row.Position, booking).Description = S["The resource's place among the appointment's resources, starting at 1."].Value;

        AddField(SchedulingReportDataSets.AppointmentTypeIdField, S["Appointment type ID"], DataFieldType.Text, row => row.Parent.AppointmentTypeId, appointment)
            .Identifier(SchedulingReportDataSets.Reference(SchedulingReportDataSets.AppointmentTypes));
        AddField(nameof(Appointment.Status), S["Status"], DataFieldType.Text, row => row.Parent.Status, appointment);
        AddField(AppointmentsReportDataSet.StartUtcField, S["Starts"], DataFieldType.DateTime, row => row.Parent.StartUtc, appointment);
        AddField(nameof(Appointment.EndUtc), S["Ends"], DataFieldType.DateTime, row => row.Parent.EndUtc, appointment);
        AddField("DurationMinutes", S["Duration (minutes)"], DataFieldType.Integer, row => SchedulingReportDataSets.Minutes(row.Parent.StartUtc, row.Parent.EndUtc), appointment);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<AppointmentIndex, DateTime>> Column)? DateColumn
        => (AppointmentsReportDataSet.StartUtcField, index => index.StartUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AppointmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AppointmentIndex, string>>>(StringComparer.Ordinal)
        {
            [SchedulingReportDataSets.AppointmentIdField] = index => index.ItemId,
            [SchedulingReportDataSets.AppointmentTypeIdField] = index => index.AppointmentTypeId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<AppointmentIndex> columns)
    {
        columns.Enum(nameof(Appointment.Status), index => index.Status);
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Appointment, string>> GetRows(Appointment document)
    {
        return ReportChildRow<Appointment, string>.For(document, document.ResourceIds?.Where(id => !string.IsNullOrEmpty(id)));
    }
}
