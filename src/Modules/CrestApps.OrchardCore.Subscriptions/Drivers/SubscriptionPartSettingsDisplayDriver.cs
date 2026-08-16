using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays and updates subscription part content type settings.
/// </summary>
public sealed class SubscriptionPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<SubscriptionPart>
{
    /// <summary>
    /// Builds the subscription part settings editor for a content type part definition.
    /// </summary>
    /// <param name="contentTypePartDefinition">The content type part definition whose settings are edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the subscription part settings editor.</returns>
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<SubscriptionPartSettings>("SubscriptionPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<SubscriptionPartSettings>();

            model.ContentTypes = settings.ContentTypes;
        }).Location("Content");
    }

    /// <summary>
    /// Updates subscription part settings on the content type part definition builder.
    /// </summary>
    /// <param name="contentTypePartDefinition">The content type part definition whose settings are updated.</param>
    /// <param name="context">The type part editor update context.</param>
    /// <returns>The display result that renders the updated subscription part settings editor.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var settings = new SubscriptionPartSettings();

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        context.Builder.WithSettings(settings);

        return Edit(contentTypePartDefinition, context);
    }
}
