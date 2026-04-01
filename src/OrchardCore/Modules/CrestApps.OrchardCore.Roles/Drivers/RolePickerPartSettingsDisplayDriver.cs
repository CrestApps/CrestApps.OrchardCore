using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Roles.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Roles.Drivers;

internal sealed class RolePickerPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<RolePickerPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<RolePickerPartSettingsViewModel>("RolePickerPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<RolePickerPartSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.AllowSelectMultiple = settings.AllowSelectMultiple;
            model.ExcludedRoles = settings.ExcludedRoles;
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
