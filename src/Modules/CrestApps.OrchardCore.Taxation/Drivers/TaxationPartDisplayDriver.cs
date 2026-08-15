using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// Display driver for the <see cref="TaxationPart"/> editor.
/// </summary>
public sealed class TaxationPartDisplayDriver : ContentPartDisplayDriver<TaxationPart>
{
    /// <inheritdoc />
    public override IDisplayResult Edit(TaxationPart part, BuildPartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<TaxationPartSettings>();

        return Initialize<TaxationPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Taxable = context.IsNew ? true : part.Taxable;
            model.TaxCategoryCode = context.IsNew ? settings.DefaultTaxCategoryCode : part.TaxCategoryCode;
            model.TaxClassificationCode = context.IsNew ? settings.DefaultTaxClassificationCode : part.TaxClassificationCode;
            model.ExternalTaxCode = part.ExternalTaxCode;
            model.AllowClassificationOverride = settings.AllowClassificationOverride;
        });
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TaxationPart part, UpdatePartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<TaxationPartSettings>();

        var model = new TaxationPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Taxable = model.Taxable;
        part.ExternalTaxCode = model.ExternalTaxCode;

        if (settings.AllowClassificationOverride)
        {
            part.TaxCategoryCode = model.TaxCategoryCode;
            part.TaxClassificationCode = model.TaxClassificationCode;
        }
        else
        {
            part.TaxCategoryCode = settings.DefaultTaxCategoryCode;
            part.TaxClassificationCode = settings.DefaultTaxClassificationCode;
        }

        return Edit(part, context);
    }
}
