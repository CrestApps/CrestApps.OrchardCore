using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Roles.Drivers;

internal sealed class RolePickerPartDisplayDriver : ContentPartDisplayDriver<RolePickerPart>
{
    private readonly RoleManager<IRole> _roleManager;

    internal readonly IStringLocalizer S;

    public RolePickerPartDisplayDriver(
        RoleManager<IRole> roleManager,
        IStringLocalizer<RolePickerPartDisplayDriver> stringLocalizer)
    {
        _roleManager = roleManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RolePickerPart part, BuildPartEditorContext context)
    {
        return Initialize<RolePickerViewModel>(GetEditorShapeType(context), m =>
        {
            var settings = context.TypePartDefinition.GetSettings<RolePickerPartSettings>();

            m.DisplayName = context.TypePartDefinition.DisplayName();
            m.Settings = settings;
            m.Roles = part.RoleNames;

            if (!settings.AllowSelectMultiple)
            {
                m.AvailableRoles = _roleManager.Roles
                    .Select(role => role.RoleName)
                    .Except(m.Settings.ExcludedRoles ?? [])
                    .Order()
                    .Select(x => new SelectListItem(x, x))
                    .ToArray();
            }
        });
    }
    public override async Task<IDisplayResult> UpdateAsync(RolePickerPart part, UpdatePartEditorContext context)
    {
        var model = new RolePickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = context.TypePartDefinition.GetSettings<RolePickerPartSettings>();

        var selectedRoles = model.Roles.Except(settings.ExcludedRoles ?? []).ToArray();

        if (settings.Required && selectedRoles.Length == 0)
        {
            if (settings.AllowSelectMultiple)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["You must select at least one role."]);
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["You must select a role."]);
            }
        }

        if (!settings.AllowSelectMultiple && selectedRoles.Length > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Roles), S["Only one roles is allowed."]);
        }

        part.RoleNames = selectedRoles;

        return Edit(part, context);
    }
}
