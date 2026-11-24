using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// The rules of the scheduling resource editor: the hours a new resource starts with, what the editor shows for a resource,
/// what it refuses, and how it applies what was entered. The editor and the scheduling AI tools both use them.
/// </summary>
internal static class SchedulingResourceEditorRules
{
    /// <summary>
    /// Creates a new resource that works Monday to Friday, 09:00 to 17:00.
    /// </summary>
    /// <returns>The resource.</returns>
    public static SchedulingResource NewResource()
    {
        var resource = new SchedulingResource();

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "09:00", End = "17:00" });
        }

        return resource;
    }

    /// <summary>
    /// Fills the editor's fields from a resource.
    /// </summary>
    /// <param name="model">The editor's fields.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="defaultTimeZoneId">The time zone a resource without one starts in: the site's.</param>
    public static void Fill(SchedulingResourceViewModel model, SchedulingResource resource, string defaultTimeZoneId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(resource);

        model.Name = resource.Name;
        model.Kind = resource.Kind.ToString();
        model.UserId = resource.UserId;
        model.CalendarId = resource.CalendarId;
        model.TimeZoneId = resource.TimeZoneId ?? defaultTimeZoneId;
        model.AvailabilityMode = resource.AvailabilityMode.ToString();
        model.ShiftCalendarIds = (resource.ShiftCalendarIds ?? []).Append(resource.ShiftCalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        model.WorkingHours = SchedulingFormat.WriteHours(resource.WorkingHours);
        model.DateOverrides = SchedulingFormat.WriteOverrides(resource.DateOverrides);
        model.Enabled = resource.Enabled;
    }

    /// <summary>
    /// Checks what was entered and applies it to the resource. The resource is changed even when something is refused, as
    /// the editor does; the caller decides not to save it.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="posted">What was entered.</param>
    /// <param name="S">The localizer of the messages.</param>
    /// <returns>What was refused, each with the name of the editor field it belongs to.</returns>
    public static IReadOnlyList<KeyValuePair<string, LocalizedString>> Apply(SchedulingResource resource, SchedulingResourceViewModel posted, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(posted);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<KeyValuePair<string, LocalizedString>>();
        var unreadable = new List<string>();
        var hours = SchedulingFormat.ReadHours(posted.WorkingHours, unreadable);
        var overrides = SchedulingFormat.ReadOverrides(posted.DateOverrides, unreadable);

        if (string.IsNullOrWhiteSpace(posted.Name))
        {
            errors.Add(new(nameof(posted.Name), S["The name is required."]));
        }

        foreach (var line in unreadable)
        {
            errors.Add(new(nameof(posted.WorkingHours), S["This line can't be read: {0}", line]));
        }

        if (!string.IsNullOrWhiteSpace(posted.TimeZoneId) && !CalendarTime.IsValidZone(posted.TimeZoneId))
        {
            errors.Add(new(nameof(posted.TimeZoneId), S["'{0}' is not a time zone.", posted.TimeZoneId]));
        }

        resource.Name = posted.Name?.Trim();
        resource.Kind = Enum.TryParse<SchedulingResourceKind>(posted.Kind, out var kind) ? kind : SchedulingResourceKind.Staff;
        resource.UserId = resource.Kind == SchedulingResourceKind.Staff && !string.IsNullOrWhiteSpace(posted.UserId) ? posted.UserId.Trim() : null;
        resource.CalendarId = string.IsNullOrWhiteSpace(posted.CalendarId) ? null : posted.CalendarId;
        resource.TimeZoneId = CalendarTime.NormalizeZoneId(posted.TimeZoneId) ?? resource.TimeZoneId ?? "UTC";
        resource.AvailabilityMode = Enum.TryParse<SchedulingAvailabilityMode>(posted.AvailabilityMode, out var mode) ? mode : SchedulingAvailabilityMode.WorkingHours;
        resource.ShiftCalendarIds = (posted.ShiftCalendarIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        resource.ShiftCalendarId = null;
        resource.WorkingHours = hours;
        resource.DateOverrides = overrides;
        resource.Enabled = posted.Enabled;

        return errors;
    }
}
