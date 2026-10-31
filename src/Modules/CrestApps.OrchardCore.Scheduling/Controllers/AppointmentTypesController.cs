using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The list of appointment types and their editor.
/// </summary>
public sealed partial class AppointmentTypesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTypesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentTypesController(
        IAuthorizationService authorizationService,
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        INotifier notifier,
        IHtmlLocalizer<AppointmentTypesController> htmlLocalizer,
        IStringLocalizer<AppointmentTypesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the appointment types.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/types", "SchedulingAppointmentTypes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var types = await _typeStore.ListAsync();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new SchedulingListViewModel
        {
            List = new AdminListContext<AppointmentType>
            {
                Source = types.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, types.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing appointment type.
    /// </summary>
    /// <param name="id">The type's id, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("scheduling/types/edit/{id?}", "SchedulingAppointmentTypesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var type = string.IsNullOrEmpty(id) ? new AppointmentType() : await _typeStore.FindByIdAsync(id);

        if (type is null)
        {
            return NotFound();
        }

        var model = new AppointmentTypeViewModel
        {
            Id = type.ItemId,
            Name = type.Name,
            Description = type.Description,
            Slug = type.Slug,
            DurationMinutes = type.DurationMinutes,
            SlotIntervalMinutes = type.SlotIntervalMinutes,
            BufferBeforeMinutes = type.BufferBeforeMinutes,
            BufferAfterMinutes = type.BufferAfterMinutes,
            MinimumNoticeMinutes = type.MinimumNoticeMinutes,
            HorizonDays = type.HorizonDays,
            MaxPerDay = type.MaxPerDay,
            MaxPerWeek = type.MaxPerWeek,
            MaxUpcomingPerCustomer = type.MaxUpcomingPerCustomer,
            CancelNoticeMinutes = type.CancelNoticeMinutes,
            RescheduleNoticeMinutes = type.RescheduleNoticeMinutes,
            Capacity = type.Capacity,
            Requirements = type.GetResourceRequirements()
                .Where(requirement => requirement.ResourceIds.Count > 0)
                .Select(requirement => new AppointmentResourceRequirementViewModel
                {
                    Name = requirement.Name,
                    ResourceIds = requirement.ResourceIds.ToArray(),
                    Assignment = requirement.Assignment.ToString(),
                    Weights = requirement.ResourceIds.ToDictionary(id => id, requirement.GetWeight, StringComparer.Ordinal),
                })
                .ToList(),
            RequiresConfirmation = type.RequiresConfirmation,
            HoldMinutes = type.HoldMinutes,
            AllowSelfService = type.AllowSelfService,
            Location = type.Location,
            Color = type.Color ?? "#0b7285",
            Enabled = type.Enabled,
        };

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves an appointment type.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("scheduling/types/edit/{id?}", "SchedulingAppointmentTypesEdit")]
    public async Task<IActionResult> EditPost(AppointmentTypeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var type = string.IsNullOrEmpty(model.Id) ? new AppointmentType() : await _typeStore.FindByIdAsync(model.Id);

        if (type is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["The name is required."]);
        }

        if (model.DurationMinutes < 5 || model.SlotIntervalMinutes < 5)
        {
            ModelState.AddModelError(nameof(model.DurationMinutes), S["The length and the time between start times must be at least 5 minutes."]);
        }

        var slug = string.IsNullOrWhiteSpace(model.Slug) ? null : SlugPattern().Replace(model.Slug.Trim().ToLowerInvariant(), "-").Trim('-');
        var requirements = ToRequirements(model.Requirements);

        if (requirements.SelectMany(requirement => requirement.ResourceIds).GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            ModelState.AddModelError(nameof(model.Requirements), S["A resource can be in only one group."]);
        }

        if (slug is not null && await _typeStore.FindBySlugAsync(slug) is { } other && other.ItemId != type.ItemId)
        {
            ModelState.AddModelError(nameof(model.Slug), S["Another appointment type uses this address."]);
        }

        if (!ModelState.IsValid)
        {
            await FillAsync(model);

            return View(model);
        }

        type.Name = model.Name.Trim();
        type.Description = model.Description?.Trim();
        type.Slug = slug;
        type.DurationMinutes = model.DurationMinutes;
        type.SlotIntervalMinutes = model.SlotIntervalMinutes;
        type.BufferBeforeMinutes = Math.Max(0, model.BufferBeforeMinutes);
        type.BufferAfterMinutes = Math.Max(0, model.BufferAfterMinutes);
        type.MinimumNoticeMinutes = Math.Max(0, model.MinimumNoticeMinutes);
        type.HorizonDays = Math.Clamp(model.HorizonDays, 1, 730);
        type.MaxPerDay = Math.Max(0, model.MaxPerDay);
        type.MaxPerWeek = Math.Max(0, model.MaxPerWeek);
        type.MaxUpcomingPerCustomer = Math.Max(0, model.MaxUpcomingPerCustomer);
        type.CancelNoticeMinutes = Math.Max(0, model.CancelNoticeMinutes);
        type.RescheduleNoticeMinutes = Math.Max(0, model.RescheduleNoticeMinutes);
        type.Capacity = Math.Max(1, model.Capacity);
        // One unnamed group without weights is stored the simple way, as the type's resources and assignment, so it reads the
        // same as before groups existed. Several groups are stored as requirements, with every resource in ResourceIds too.
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
        type.RequiresConfirmation = model.RequiresConfirmation;
        type.HoldMinutes = Math.Clamp(model.HoldMinutes, 1, 1440);
        type.AllowSelfService = model.AllowSelfService;
        type.Location = model.Location?.Trim();
        type.Color = model.Color;
        type.Enabled = model.Enabled;

        if (string.IsNullOrEmpty(type.ItemId))
        {
            await _typeStore.CreateAsync(type);
        }
        else
        {
            await _typeStore.UpdateAsync(type);
        }

        await _notifier.SuccessAsync(H["The appointment type has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an appointment type.
    /// </summary>
    /// <param name="id">The type's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/types/delete/{id}", "SchedulingAppointmentTypesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var type = await _typeStore.FindByIdAsync(id);

        if (type is not null)
        {
            await _typeStore.DeleteAsync(type);
            await _notifier.SuccessAsync(H["The appointment type has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
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
