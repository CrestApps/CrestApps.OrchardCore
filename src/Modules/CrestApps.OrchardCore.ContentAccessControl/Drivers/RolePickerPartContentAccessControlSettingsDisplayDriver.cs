using CrestApps.OrchardCore.ContentAccessControl.ViewModels;
using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContentAccessControl.Drivers;

internal sealed class RolePickerPartContentAccessControlSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<RolePickerPart>
{
    internal readonly IStringLocalizer S;

    public RolePickerPartContentAccessControlSettingsDisplayDriver(IStringLocalizer<RolePickerPartContentAccessControlSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<RolePickerPartContentAccessControlSettingsViewModel>("RolePickerPartContentAccessControlSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<RolePickerPartContentAccessControlSettings>();

            model.IsContentRestricted = settings.IsContentRestricted;
        }).Location(l => l.Zone("Content", "6").Card(S["Access Control"], "60"));
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
