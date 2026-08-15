using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
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
    private readonly ITaxCategoryStore _categoryStore;

    internal readonly IStringLocalizer S;

    public TaxationPartSettingsDisplayDriver(
        ITaxCategoryStore categoryStore,
        IStringLocalizer<TaxationPartSettingsDisplayDriver> stringLocalizer)
    {
        _categoryStore = categoryStore;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<TaxationPartSettingsViewModel>("TaxationPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<TaxationPartSettings>();

            model.DefaultTaxCategoryCode = settings.DefaultTaxCategoryCode;
            model.DefaultTaxClassificationCode = settings.DefaultTaxClassificationCode;
            model.AllowClassificationOverride = settings.AllowClassificationOverride;
            model.TaxCategories = await BuildCategoryListAsync();
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
