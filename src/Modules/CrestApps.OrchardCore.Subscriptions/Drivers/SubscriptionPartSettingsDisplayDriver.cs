using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<SubscriptionsPart>
{
    public override Task<IDisplayResult> EditAsync(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Task.FromResult<IDisplayResult>(
            Initialize<SubscriptionPartSettings>("SubscriptionPartSettings_Edit", model =>
            {
                var settings = contentTypePartDefinition.GetSettings<SubscriptionPartSettings>();

                model.ContentTypes = settings.ContentTypes;
            }).Location("Content")
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var settings = new SubscriptionPartSettings();

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        context.Builder.WithSettings(settings);

        return Edit(contentTypePartDefinition);
    }
}
