using CrestApps.OrchardCore.ContentAccessControl.ViewModels;
using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentAccessControl.Drivers;

internal sealed class RolePickerPartContentAccessControlSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<RolePickerPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<RolePickerPartContentAccessControlSettingsViewModel>("RolePickerPartContentAccessControlSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<RolePickerPartContentAccessControlSettings>();

            model.IsContentRestricted = settings.IsContentRestricted;
        }).Location("Content:6");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new RolePickerPartContentAccessControlSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(
            new RolePickerPartContentAccessControlSettings
            {
                IsContentRestricted = model.IsContentRestricted,
            });

        return Edit(contentTypePartDefinition, context);
    }
}
