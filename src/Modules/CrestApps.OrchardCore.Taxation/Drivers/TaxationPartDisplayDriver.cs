using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
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
    private readonly INamedCatalog<TaxCategory> _categoryStore;

    internal readonly IStringLocalizer S;

    public TaxationPartDisplayDriver(
        INamedCatalog<TaxCategory> categoryStore,
        IStringLocalizer<TaxationPartDisplayDriver> stringLocalizer)
    {
        _categoryStore = categoryStore;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TaxationPart part, BuildPartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<TaxationPartSettings>();

        return Initialize<TaxationPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Taxable = context.IsNew ? true : part.Taxable;
            model.TaxCategoryCode = context.IsNew ? settings.DefaultTaxCategoryCode : part.TaxCategoryCode;
            model.TaxClassificationCode = context.IsNew ? settings.DefaultTaxClassificationCode : part.TaxClassificationCode;
            model.ExternalTaxCode = part.ExternalTaxCode;
            model.AllowClassificationOverride = settings.AllowClassificationOverride;
            model.ShowClassification = settings.ShowClassification;
            model.ShowExternalTaxCode = settings.ShowExternalTaxCode;

            if (settings.AllowClassificationOverride)
            {
                model.TaxCategories = await BuildCategoryListAsync();
            }
        });
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TaxationPart part, UpdatePartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<TaxationPartSettings>();

        var model = new TaxationPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // An editor that was not on the form posts nothing; the part keeps what it has rather than being cleared.
        if (!model.Rendered)
        {
            return Edit(part, context);
        }

        part.Taxable = model.Taxable;

        if (settings.ShowExternalTaxCode)
        {
            part.ExternalTaxCode = model.ExternalTaxCode;
        }

        if (settings.AllowClassificationOverride)
        {
            part.TaxCategoryCode = model.TaxCategoryCode;

            if (settings.ShowClassification)
            {
                part.TaxClassificationCode = model.TaxClassificationCode;
            }
        }
        else
        {
            part.TaxCategoryCode = settings.DefaultTaxCategoryCode;
            part.TaxClassificationCode = settings.DefaultTaxClassificationCode;
        }

        return Edit(part, context);
    }

    private async Task<IList<SelectListItem>> BuildCategoryListAsync()
    {
        var categories = await _categoryStore.GetAllAsync();

        return
        [
            new SelectListItem(S["None"], string.Empty),
            .. categories
                .Where(category => !string.IsNullOrEmpty(category.Code))
                .OrderBy(category => category.Name)
                .Select(category => new SelectListItem($"{category.Name} ({category.Code})", category.Code)),
        ];
    }
}
