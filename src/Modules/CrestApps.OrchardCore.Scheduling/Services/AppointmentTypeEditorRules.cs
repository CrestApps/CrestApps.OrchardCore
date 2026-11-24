using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// The rules of the appointment type editor: what it shows for a type, what it refuses, and how it applies what was entered.
/// The editor's display driver and the scheduling AI tools both use them, so a type saved either way is checked the same way.
/// </summary>
internal static partial class AppointmentTypeEditorRules
{
    /// <summary>
    /// Fills the editor's fields from an appointment type.
    /// </summary>
    /// <param name="model">The editor's fields.</param>
    /// <param name="type">The appointment type.</param>
    public static void Fill(AppointmentTypeViewModel model, AppointmentType type)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(type);

        model.Name = type.Name;
        model.Description = type.Description;
        model.Slug = type.Slug;
        model.DurationMinutes = type.DurationMinutes;
        model.SlotIntervalMinutes = type.SlotIntervalMinutes;
        model.BufferBeforeMinutes = type.BufferBeforeMinutes;
        model.BufferAfterMinutes = type.BufferAfterMinutes;
        model.MinimumNoticeMinutes = type.MinimumNoticeMinutes;
        model.HorizonDays = type.HorizonDays;
        model.MaxPerDay = type.MaxPerDay;
        model.MaxPerWeek = type.MaxPerWeek;
        model.MaxUpcomingPerCustomer = type.MaxUpcomingPerCustomer;
        model.CancelNoticeMinutes = type.CancelNoticeMinutes;
        model.RescheduleNoticeMinutes = type.RescheduleNoticeMinutes;
        model.Capacity = type.Capacity;
        model.Requirements = type.GetResourceRequirements()
            .Where(requirement => requirement.ResourceIds.Count > 0)
            .Select(requirement => new AppointmentResourceRequirementViewModel
            {
                Name = requirement.Name,
                ResourceIds = requirement.ResourceIds.ToArray(),
                Assignment = requirement.Assignment.ToString(),
                Weights = requirement.ResourceIds.ToDictionary(id => id, requirement.GetWeight, StringComparer.Ordinal),
            })
            .ToList();
        model.RequiresConfirmation = type.RequiresConfirmation;
        model.HoldMinutes = type.HoldMinutes;
        model.AllowSelfService = type.AllowSelfService;
        model.Location = type.Location;
        model.Color = type.Color ?? "#0b7285";
        model.Enabled = type.Enabled;
    }

    /// <summary>
    /// Checks what was entered and applies it to the appointment type. The type is changed even when something is refused,
    /// as the editor does; the caller decides not to save it.
    /// </summary>
    /// <param name="type">The appointment type.</param>
    /// <param name="posted">What was entered.</param>
    /// <param name="typeStore">The appointment type store, to check the booking address is free.</param>
    /// <param name="S">The localizer of the messages.</param>
    /// <returns>What was refused, each with the name of the editor field it belongs to.</returns>
    public static async Task<IReadOnlyList<KeyValuePair<string, LocalizedString>>> ApplyAsync(AppointmentType type, AppointmentTypeViewModel posted, AppointmentTypeStore typeStore, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(posted);
        ArgumentNullException.ThrowIfNull(typeStore);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<KeyValuePair<string, LocalizedString>>();

        if (string.IsNullOrWhiteSpace(posted.Name))
        {
            errors.Add(new(nameof(posted.Name), S["The name is required."]));
        }

        if (posted.DurationMinutes < 5 || posted.SlotIntervalMinutes < 5)
        {
            errors.Add(new(nameof(posted.DurationMinutes), S["The length and the time between start times must be at least 5 minutes."]));
        }

        var slug = string.IsNullOrWhiteSpace(posted.Slug) ? null : SlugPattern().Replace(posted.Slug.Trim().ToLowerInvariant(), "-").Trim('-');
        var requirements = ToRequirements(posted.Requirements);

        if (requirements.SelectMany(requirement => requirement.ResourceIds).GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add(new(nameof(posted.Requirements), S["A resource can be in only one group."]));
        }

        if (slug is not null && await typeStore.FindBySlugAsync(slug) is { } other && other.ItemId != type.ItemId)
        {
            errors.Add(new(nameof(posted.Slug), S["Another appointment type uses this address."]));
        }

        type.Name = posted.Name?.Trim();
        type.Description = posted.Description?.Trim();
        type.Slug = slug;
        type.DurationMinutes = posted.DurationMinutes;
        type.SlotIntervalMinutes = posted.SlotIntervalMinutes;
        type.BufferBeforeMinutes = Math.Max(0, posted.BufferBeforeMinutes);
        type.BufferAfterMinutes = Math.Max(0, posted.BufferAfterMinutes);
        type.MinimumNoticeMinutes = Math.Max(0, posted.MinimumNoticeMinutes);
        type.HorizonDays = Math.Clamp(posted.HorizonDays, 1, 730);
        type.MaxPerDay = Math.Max(0, posted.MaxPerDay);
        type.MaxPerWeek = Math.Max(0, posted.MaxPerWeek);
        type.MaxUpcomingPerCustomer = Math.Max(0, posted.MaxUpcomingPerCustomer);
        type.CancelNoticeMinutes = Math.Max(0, posted.CancelNoticeMinutes);
        type.RescheduleNoticeMinutes = Math.Max(0, posted.RescheduleNoticeMinutes);
        type.Capacity = Math.Max(1, posted.Capacity);

        // One unnamed group without weights is stored the simple way, as the type's resources and assignment, so it reads
        // the same as before groups existed. Several groups are stored as requirements, with every resource in ResourceIds
        // too.
        if (requirements.Count <= 1 && requirements.All(requirement => string.IsNullOrEmpty(requirement.Name) && requirement.Weights.Count == 0))
        {
            type.ResourceIds = requirements.FirstOrDefault()?.ResourceIds ?? [];
            type.Assignment = requirements.FirstOrDefault()?.Assignment ?? AppointmentAssignment.AnyAvailable;
            type.ResourceRequirements = [];
        }
        else
        {
            type.ResourceRequirements = requirements;
            type.ResourceIds = requirements.SelectMany(requirement => requirement.ResourceIds).Distinct(StringComparer.Ordinal).ToList();
            type.Assignment = requirements[0].Assignment;
        }

        type.RequiresConfirmation = posted.RequiresConfirmation;
        type.HoldMinutes = Math.Clamp(posted.HoldMinutes, 1, 1440);
        type.AllowSelfService = posted.AllowSelfService;
        type.Location = posted.Location?.Trim();
        type.Color = posted.Color;
        type.Enabled = posted.Enabled;

        return errors;
    }

    private static List<AppointmentResourceRequirement> ToRequirements(IEnumerable<AppointmentResourceRequirementViewModel> groups)
    {
        var requirements = new List<AppointmentResourceRequirement>();

        foreach (var group in groups ?? [])
        {
            var resourceIds = (group?.ResourceIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

            if (resourceIds.Count == 0)
            {
                continue;
            }

            var assignment = Enum.TryParse<AppointmentAssignment>(group.Assignment, out var parsed) ? parsed : AppointmentAssignment.AnyAvailable;

            requirements.Add(new AppointmentResourceRequirement
            {
                Name = string.IsNullOrWhiteSpace(group.Name) ? null : group.Name.Trim(),
                ResourceIds = resourceIds,
                Assignment = assignment,

                // Weights only matter for round robin, and 1 is the default, so only the higher ones are kept.
                Weights = assignment == AppointmentAssignment.RoundRobin
                    ? (group.Weights ?? []).Where(weight => resourceIds.Contains(weight.Key) && weight.Value > 1).ToDictionary(weight => weight.Key, weight => Math.Min(weight.Value, 100), StringComparer.Ordinal)
                    : new Dictionary<string, int>(StringComparer.Ordinal),
            });
        }

        return requirements;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
