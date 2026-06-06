using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentFields.Drivers;

/// <summary>
/// Display driver for the <see cref="PhoneFieldSettings"/> configuration.
/// </summary>
public sealed class PhoneFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<PhoneField>
{
    /// <summary>
    /// Builds the edit shape for phone field settings.
    /// </summary>
    /// <param name="model">The part field definition.</param>
    /// <param name="context">The build editor context.</param>
    /// <returns>The display result.</returns>
    public override IDisplayResult Edit(ContentPartFieldDefinition model, BuildEditorContext context)
    {
        return Initialize<PhoneFieldSettings>("PhoneFieldSettings_Edit", settings =>
        {
            var fieldSettings = model.GetSettings<PhoneFieldSettings>();

            settings.Hint = fieldSettings.Hint;
            settings.Required = fieldSettings.Required;
            settings.DefaultCountryCode = fieldSettings.DefaultCountryCode;
        }).Location("Content");
    }

    /// <summary>
    /// Updates the phone field settings from the form submission.
    /// </summary>
    /// <param name="model">The part field definition.</param>
    /// <param name="context">The update context.</param>
    /// <returns>The display result.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        var settings = new PhoneFieldSettings();

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        context.Builder.WithSettings(settings);

        return Edit(model, context);
    }
}
