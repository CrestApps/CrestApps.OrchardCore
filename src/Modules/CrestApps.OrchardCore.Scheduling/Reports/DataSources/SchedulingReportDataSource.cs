using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Scheduling.Reports.DataSources;

/// <summary>
/// Exposes the appointments, the resources they book, the appointment types, the resources that can be booked, and the
/// waitlist as a data source of Orchard Core, for reports and data pipelines. Each data set checks that the principal
/// holds the scheduling permission its admin screen asks for.
/// </summary>
internal sealed class SchedulingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingReportDataSource"/> class.
    /// </summary>
    /// <param name="appointments">The appointments data set.</param>
    /// <param name="appointmentResources">The appointment resources data set.</param>
    /// <param name="appointmentTypes">The appointment types data set.</param>
    /// <param name="resources">The scheduling resources data set.</param>
    /// <param name="waitlist">The waitlist data set.</param>
    public SchedulingReportDataSource(
        AppointmentsReportDataSet appointments,
        AppointmentResourcesReportDataSet appointmentResources,
        AppointmentTypesReportDataSet appointmentTypes,
        SchedulingResourcesReportDataSet resources,
        WaitlistReportDataSet waitlist)
    {
        _dataSets = [appointments, appointmentResources, appointmentTypes, resources, waitlist];
    }

    /// <inheritdoc/>
    public static string Name => SchedulingReportDataSets.Source;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SchedulingReportDataSource>("Scheduling");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<SchedulingReportDataSource>("Appointments with their customers, types and resources, the appointment types, the staff, rooms and equipment that can be booked, and the waitlist.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
