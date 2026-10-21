using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

internal sealed class CurrencyEntryDisplayDriver : DisplayDriver<CurrencyEntry>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyEntryDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyEntryDisplayDriver(IStringLocalizer<CurrencyEntryDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CurrencyEntry model, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{model.Name} {model.DisplayName}".ToLowerInvariant();
        }

        return CombineAsync(
            View("CurrencyEntry_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CurrencyEntry_DefaultTags_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Code:5"),
            View("CurrencyEntry_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }

    public override IDisplayResult Edit(CurrencyEntry model, BuildEditorContext context)
    {
        return Initialize<CurrencyEntryViewModel>("CurrencyEntryFields_Edit", viewModel =>
        {
            viewModel.IsNew = context.IsNew;
            viewModel.Name = model.Name;
            viewModel.DisplayName = model.DisplayName;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(CurrencyEntry model, UpdateEditorContext context)
    {
        var viewModel = new CurrencyEntryViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var normalizedCode = CurrencyCodeUtility.Normalize(viewModel.Name);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Currency code is a required field."]);
        }
        else if (!CurrencyCodeUtility.IsValid(normalizedCode))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Currency code must be a three-letter ISO-4217 code."]);
        }

        if (string.IsNullOrWhiteSpace(viewModel.DisplayName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.DisplayName), S["Display name is a required field."]);
        }

        if (context.IsNew)
        {
            model.Name = normalizedCode;
        }

        model.DisplayName = viewModel.DisplayName?.Trim();

        return Edit(model, context);
    }
}
