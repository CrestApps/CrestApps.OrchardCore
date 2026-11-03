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
/// One row per resource that can be booked: a staff member, a room, a piece of equipment or another resource, with
/// the calendar its bookings go on and where its working time comes from. Working hours and their exceptions are not
/// exposed.
/// </summary>
internal sealed class SchedulingResourcesReportDataSet : IndexedReportDataSet<SchedulingResource, SchedulingResourceIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourcesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session resources are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulingResourcesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<SchedulingResourcesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                SchedulingReportDataSets.Resources,
                stringLocalizer["Scheduling resources"],
                stringLocalizer["One row per staff member, room or piece of equipment that can be booked, with its calendar."]),
            session,
            authorizationService,
            SchedulingConstants.CollectionName,
            SchedulingPermissions.ViewAppointments)
    {
        var S = stringLocalizer;
        var resource = S["Resource"].Value;

        AddField(SchedulingReportDataSets.ItemIdField, S["Resource ID"], DataFieldType.Text, record => record.ItemId, resource).Identifier();
        AddField(nameof(SchedulingResource.Name), S["Name"], DataFieldType.Text, record => record.Name, resource);
        AddField(nameof(SchedulingResource.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, resource).Description = S["Staff, Room, Equipment or Other."].Value;
        AddField(nameof(SchedulingResource.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, resource)
            .Identifier(ReportDataReferences.User())
            .Description = S["The user a staff member is; empty for other resources."].Value;
        AddField(nameof(SchedulingResource.CalendarId), S["Calendar ID"], DataFieldType.Text, record => record.CalendarId, resource)
            .Identifier(SchedulingReportDataSets.CalendarReference("Calendars"))
            .Description = S["The calendar its bookings go on."].Value;
        AddField(nameof(SchedulingResource.AvailabilityMode), S["Available by"], DataFieldType.Text, record => record.AvailabilityMode, resource).Description = S["WorkingHours, or Shifts when its shifts on a calendar say when it works."].Value;
        AddField(nameof(SchedulingResource.ShiftCalendarId), S["Shift calendar ID"], DataFieldType.Text, record => record.ShiftCalendarId, resource)
            .Identifier(SchedulingReportDataSets.CalendarReference("Calendars"));
        AddField(nameof(SchedulingResource.TimeZoneId), S["Time zone"], DataFieldType.Text, record => record.TimeZoneId, resource);
        AddField(nameof(SchedulingResource.Enabled), S["Enabled"], DataFieldType.Boolean, record => record.Enabled, resource);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<SchedulingResourceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<SchedulingResourceIndex, string>>>(StringComparer.Ordinal)
        {
            [SchedulingReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(SchedulingResource.UserId)] = index => index.UserId,
        };
}
