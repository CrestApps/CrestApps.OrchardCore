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
/// One row per appointment type: how long it lasts, its booking rules, how its resources are assigned, and whether
/// customers can book it themselves.
/// </summary>
internal sealed class AppointmentTypesReportDataSet : IndexedReportDataSet<AppointmentType, AppointmentTypeIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTypesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session appointment types are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentTypesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AppointmentTypesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                SchedulingReportDataSets.AppointmentTypes,
                stringLocalizer["Appointment types"],
                stringLocalizer["One row per appointment type: its length, booking rules, resources and whether customers can book it themselves."]),
            session,
            authorizationService,
            SchedulingConstants.CollectionName,
            SchedulingPermissions.ViewAppointments)
    {
        var S = stringLocalizer;
        var type = S["Appointment type"].Value;
        var rules = S["Booking rules"].Value;

        AddField(SchedulingReportDataSets.ItemIdField, S["Appointment type ID"], DataFieldType.Text, record => record.ItemId, type).Identifier();
        AddField(nameof(AppointmentType.Name), S["Name"], DataFieldType.Text, record => record.Name, type);
        AddField(nameof(AppointmentType.Slug), S["Booking page slug"], DataFieldType.Text, record => record.Slug, type);
        AddField(nameof(AppointmentType.Location), S["Location"], DataFieldType.Text, record => record.Location, type);
        AddField(nameof(AppointmentType.Enabled), S["Enabled"], DataFieldType.Boolean, record => record.Enabled, type);
        AddField(nameof(AppointmentType.AllowSelfService), S["Customers can book"], DataFieldType.Boolean, record => record.AllowSelfService, type);
        AddField(nameof(AppointmentType.RequiresConfirmation), S["Needs confirmation"], DataFieldType.Boolean, record => record.RequiresConfirmation, type);
        AddField(nameof(AppointmentType.Assignment), S["Assignment"], DataFieldType.Text, record => record.Assignment, type).Description = S["How resources are picked: AnyAvailable, RoundRobin or AllTogether."].Value;
        AddField(nameof(AppointmentType.ResourceIds), S["Resource IDs"], DataFieldType.Text, record => SchedulingReportDataSets.Join(record.ResourceIds), type);
        AddField("ResourceCount", S["Resources"], DataFieldType.Integer, record => record.ResourceIds?.Count ?? 0, type);

        AddField(nameof(AppointmentType.DurationMinutes), S["Duration (minutes)"], DataFieldType.Integer, record => record.DurationMinutes, rules);
        AddField(nameof(AppointmentType.SlotIntervalMinutes), S["Start times every (minutes)"], DataFieldType.Integer, record => record.SlotIntervalMinutes, rules);
        AddField(nameof(AppointmentType.BufferBeforeMinutes), S["Free time before (minutes)"], DataFieldType.Integer, record => record.BufferBeforeMinutes, rules);
        AddField(nameof(AppointmentType.BufferAfterMinutes), S["Free time after (minutes)"], DataFieldType.Integer, record => record.BufferAfterMinutes, rules);
        AddField(nameof(AppointmentType.MinimumNoticeMinutes), S["Minimum notice (minutes)"], DataFieldType.Integer, record => record.MinimumNoticeMinutes, rules);
        AddField(nameof(AppointmentType.HorizonDays), S["Bookable ahead (days)"], DataFieldType.Integer, record => record.HorizonDays, rules);
        AddField(nameof(AppointmentType.MaxPerDay), S["Most per resource a day"], DataFieldType.Integer, record => record.MaxPerDay, rules).Description = S["The most appointments of this type one resource takes in a day; 0 for no limit."].Value;
        AddField(nameof(AppointmentType.Capacity), S["Capacity"], DataFieldType.Integer, record => record.Capacity, rules).Description = S["How many customers share one appointment, for a class or group session."].Value;
        AddField(nameof(AppointmentType.HoldMinutes), S["Hold (minutes)"], DataFieldType.Integer, record => record.HoldMinutes, rules).Description = S["How long a time is held while a customer books it."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AppointmentTypeIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AppointmentTypeIndex, string>>>(StringComparer.Ordinal)
        {
            [SchedulingReportDataSets.ItemIdField] = index => index.ItemId,
        };
}
