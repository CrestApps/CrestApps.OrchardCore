using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// Settings display driver for the <see cref="TaxationPart"/> attached to a content type.
/// </summary>
public sealed class TaxationPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<TaxationPart>
{
    /// <inheritdoc />
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<TaxationPartSettingsViewModel>("TaxationPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<TaxationPartSettings>();

            model.DefaultTaxCategoryCode = settings.DefaultTaxCategoryCode;
            model.DefaultTaxClassificationCode = settings.DefaultTaxClassificationCode;
            model.AllowClassificationOverride = settings.AllowClassificationOverride;
        }).Location("Content");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new TaxationPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new TaxationPartSettings
        {
            DefaultTaxCategoryCode = model.DefaultTaxCategoryCode,
            DefaultTaxClassificationCode = model.DefaultTaxClassificationCode,
            AllowClassificationOverride = model.AllowClassificationOverride,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
