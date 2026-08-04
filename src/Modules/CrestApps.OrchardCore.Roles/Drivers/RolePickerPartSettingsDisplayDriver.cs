using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Roles.Drivers;

internal sealed class RolePickerPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<RolePickerPart>
{
    private readonly RoleManager<IRole> _roleManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolePickerPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="roleManager">The role manager.</param>
    public RolePickerPartSettingsDisplayDriver(RoleManager<IRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<RolePickerPartSettingsViewModel>("RolePickerPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<RolePickerPartSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.AllowSelectMultiple = settings.AllowSelectMultiple;
            model.ExcludedRoles = settings.ExcludedRoles;

            model.AvailableRoles = _roleManager.Roles
                .Select(role => role.RoleName)
                .Order()
                .Select(x => new SelectListItem(x, x))
                .ToArray();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new RolePickerPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(
            new RolePickerPartSettings
            {
                Hint = model.Hint,
                Required = model.Required,
                AllowSelectMultiple = model.AllowSelectMultiple,
                ExcludedRoles = model.ExcludedRoles ?? [],
            });

        return Edit(contentTypePartDefinition, context);
    }
}
