using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Classes.Drivers;

/// <summary>
/// The editor of the class part: the Scheduling appointment type and resources of its sessions, and the defaults new
/// sessions start from.
/// </summary>
internal sealed class ClassPartDisplayDriver : ContentPartDisplayDriver<ClassPart>
{
    private readonly AppointmentTypeStore _appointmentTypes;
    private readonly SchedulingResourceStore _resources;

    internal readonly IStringLocalizer S;

    public ClassPartDisplayDriver(
        AppointmentTypeStore appointmentTypes,
        SchedulingResourceStore resources,
        IStringLocalizer<ClassPartDisplayDriver> stringLocalizer)
    {
        _appointmentTypes = appointmentTypes;
        _resources = resources;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ClassPart part, BuildPartEditorContext context)
        => Initialize<ClassPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.AppointmentTypeId = part.AppointmentTypeId;
            model.ResourceIds = [.. part.ResourceIds ?? []];
            model.Capacity = part.Capacity;
            model.Location = part.Location;
            model.MeetingUrl = part.MeetingUrl;
            model.WaitlistMode = part.WaitlistMode;
            model.MinimumMinutes = part.MinimumMinutes;
            model.ClassItemId = part.ContentItem.ContentItemId;
            model.IsNew = context.IsNew;

            await PopulateOptionsAsync(model);
        });

    public override async Task<IDisplayResult> UpdateAsync(ClassPart part, UpdatePartEditorContext context)
    {
        var model = new ClassPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Capacity < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Capacity), S["The capacity must be at least one."]);
        }

        if (model.MinimumMinutes is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MinimumMinutes), S["The minimum minutes must be at least one, or left empty."]);
        }

        var meetingUrl = string.IsNullOrWhiteSpace(model.MeetingUrl) ? null : model.MeetingUrl.Trim();

        if (meetingUrl is not null &&
            (!Uri.TryCreate(meetingUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MeetingUrl), S["Paste a full meeting link that starts with https://."]);
        }

        if (!string.IsNullOrEmpty(model.AppointmentTypeId) && await _appointmentTypes.FindByIdAsync(model.AppointmentTypeId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AppointmentTypeId), S["Choose an appointment type that exists."]);
        }

        part.AppointmentTypeId = string.IsNullOrEmpty(model.AppointmentTypeId) ? null : model.AppointmentTypeId;
        part.ResourceIds = (model.ResourceIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        part.Capacity = model.Capacity;
        part.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        part.MeetingUrl = meetingUrl;
        part.WaitlistMode = model.WaitlistMode;
        part.MinimumMinutes = model.MinimumMinutes;

        return Edit(part, context);
    }

    private async Task PopulateOptionsAsync(ClassPartViewModel model)
    {
        var types = await _appointmentTypes.ListAsync();
        var resources = await _resources.ListAsync();

        model.AppointmentTypeOptions = types
            .Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == model.AppointmentTypeId))
            .ToList();
        model.ResourceOptions = resources
            .Select(resource => new SelectListItem(ResourceText(resource), resource.ItemId, model.ResourceIds.Contains(resource.ItemId)))
            .ToList();
        model.WaitlistModeOptions = ClassesSelectLists.WaitlistModes(S, model.WaitlistMode);
    }

    private string ResourceText(SchedulingResource resource)
        => resource.Kind switch
        {
            SchedulingResourceKind.Room => S["{0} (room)", resource.Name],
            SchedulingResourceKind.Equipment => S["{0} (equipment)", resource.Name],
            _ => resource.Name,
        };
}
