using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Scheduling.Drivers;

/// <summary>
/// The built-in sections of the appointment type editor: general details, time, the resource groups that take the
/// appointment, and the booking rules. Other modules add their own sections to the editor with drivers of their own for
/// <see cref="AppointmentType"/>.
/// </summary>
internal sealed partial class AppointmentTypeFieldsDisplayDriver : DisplayDriver<AppointmentType>
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;

    internal readonly IStringLocalizer S;

    public AppointmentTypeFieldsDisplayDriver(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        IStringLocalizer<AppointmentTypeFieldsDisplayDriver> stringLocalizer)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AppointmentType type, BuildEditorContext context)
    {
        return Initialize<AppointmentTypeViewModel>("AppointmentTypeFields_Edit", async model =>
        {
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
            model.RequireEmailConfirmation = type.RequireEmailConfirmation;
            model.HoldMinutes = type.HoldMinutes;
            model.AllowSelfService = type.AllowSelfService;
            model.Location = type.Location;
            model.Color = type.Color ?? "#0b7285";
            model.Enabled = type.Enabled;

            await FillAsync(model);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AppointmentType type, UpdateEditorContext context)
    {
        var posted = new AppointmentTypeViewModel();

        await context.Updater.TryUpdateModelAsync(posted, Prefix);

        if (string.IsNullOrWhiteSpace(posted.Name))
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.Name)), S["The name is required."]);
        }

        if (posted.DurationMinutes < 5 || posted.SlotIntervalMinutes < 5)
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.DurationMinutes)), S["The length and the time between start times must be at least 5 minutes."]);
        }

        var slug = string.IsNullOrWhiteSpace(posted.Slug) ? null : SlugPattern().Replace(posted.Slug.Trim().ToLowerInvariant(), "-").Trim('-');
        var requirements = ToRequirements(posted.Requirements);

        if (requirements.SelectMany(requirement => requirement.ResourceIds).GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.Requirements)), S["A resource can be in only one group."]);
        }

        if (slug is not null && await _typeStore.FindBySlugAsync(slug) is { } other && other.ItemId != type.ItemId)
        {
            context.Updater.ModelState.AddModelError(Key(nameof(posted.Slug)), S["Another appointment type uses this address."]);
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
        type.RequireEmailConfirmation = posted.RequireEmailConfirmation;
        type.HoldMinutes = Math.Clamp(posted.HoldMinutes, 1, 1440);
        type.AllowSelfService = posted.AllowSelfService;
        type.Location = posted.Location?.Trim();
        type.Color = posted.Color;
        type.Enabled = posted.Enabled;

        // The form shows what was posted, as typed.
        return Initialize<AppointmentTypeViewModel>("AppointmentTypeFields_Edit", async model =>
        {
            model.Name = posted.Name;
            model.Description = posted.Description;
            model.Slug = posted.Slug;
            model.DurationMinutes = posted.DurationMinutes;
            model.SlotIntervalMinutes = posted.SlotIntervalMinutes;
            model.BufferBeforeMinutes = posted.BufferBeforeMinutes;
            model.BufferAfterMinutes = posted.BufferAfterMinutes;
            model.MinimumNoticeMinutes = posted.MinimumNoticeMinutes;
            model.HorizonDays = posted.HorizonDays;
            model.MaxPerDay = posted.MaxPerDay;
            model.MaxPerWeek = posted.MaxPerWeek;
            model.MaxUpcomingPerCustomer = posted.MaxUpcomingPerCustomer;
            model.CancelNoticeMinutes = posted.CancelNoticeMinutes;
            model.RescheduleNoticeMinutes = posted.RescheduleNoticeMinutes;
            model.Capacity = posted.Capacity;
            model.Requirements = posted.Requirements;
            model.RequiresConfirmation = posted.RequiresConfirmation;
            model.RequireEmailConfirmation = posted.RequireEmailConfirmation;
            model.HoldMinutes = posted.HoldMinutes;
            model.AllowSelfService = posted.AllowSelfService;
            model.Location = posted.Location;
            model.Color = posted.Color;
            model.Enabled = posted.Enabled;

            await FillAsync(model);
        }).Location("Content:1");
    }

    private string Key(string name)
        => string.IsNullOrEmpty(Prefix) ? name : $"{Prefix}.{name}";

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

    private async Task FillAsync(AppointmentTypeViewModel model)
    {
        // Show the groups in use, then a few empty ones the page reveals one at a time.
        model.Requirements = (model.Requirements ?? []).Where(group => group?.ResourceIds?.Any(id => !string.IsNullOrEmpty(id)) == true).ToList();
        model.VisibleRequirements = Math.Max(1, model.Requirements.Count);

        while (model.Requirements.Count < model.VisibleRequirements + 3)
        {
            model.Requirements.Add(new AppointmentResourceRequirementViewModel { Assignment = nameof(AppointmentAssignment.AnyAvailable) });
        }

        model.ResourceOptions = (await _resourceStore.ListAsync())
            .Select(resource => new SelectListItem(resource.Name, resource.ItemId))
            .ToList();
        model.AssignmentOptions =
        [
            new SelectListItem(S["Any free resource"], nameof(AppointmentAssignment.AnyAvailable)),
            new SelectListItem(S["Share out (round robin)"], nameof(AppointmentAssignment.RoundRobin)),
            new SelectListItem(S["All resources together"], nameof(AppointmentAssignment.AllTogether)),
        ];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
