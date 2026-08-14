using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<OmnichannelContactPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<OmnichannelContactPartSettingsViewModel>("OmnichannelContactPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<OmnichannelContactPartSettings>();

            model.RequireTimeZone = settings.RequireTimeZone;
            model.UseDoNotCall = settings.UseDoNotCall;
            model.UseDoNotSms = settings.UseDoNotSms;
            model.UseDoNotChat = settings.UseDoNotChat;
            model.UseDoNotEmail = settings.UseDoNotEmail;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new OmnichannelContactPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new OmnichannelContactPartSettings
        {
            RequireTimeZone = model.RequireTimeZone,
            UseDoNotCall = model.UseDoNotCall,
            UseDoNotSms = model.UseDoNotSms,
            UseDoNotChat = model.UseDoNotChat,
            UseDoNotEmail = model.UseDoNotEmail,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
