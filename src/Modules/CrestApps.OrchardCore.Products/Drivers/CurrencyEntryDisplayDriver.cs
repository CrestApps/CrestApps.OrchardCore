using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
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
        return CombineAsync(
            View("CurrencyEntry_Fields_SummaryAdmin", model).Location("Content:1"),
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
