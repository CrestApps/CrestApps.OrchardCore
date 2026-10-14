using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Deployments;

internal sealed class CurrencyDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, CurrencyDeploymentStep>
{
    private readonly INamedCatalog<CurrencyEntry> _catalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="catalog">The currency catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyDeploymentStepDisplayDriver(
        INamedCatalog<CurrencyEntry> catalog,
        IStringLocalizer<CurrencyDeploymentStepDisplayDriver> stringLocalizer)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CurrencyDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("CurrencyDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("CurrencyDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(CurrencyDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<CurrencyDeploymentStepViewModel>("CurrencyDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Currencies = (await _catalog.GetAllAsync())
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CurrencyDeploymentStepEntryViewModel
                {
                    ItemId = x.ItemId,
                    Name = x.Name,
                    DisplayName = x.DisplayName,
                    IsSelected = step.CurrencyIds?.Contains(x.ItemId) ?? false,
                })
                .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(CurrencyDeploymentStep step, UpdateEditorContext context)
    {
        var model = new CurrencyDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            viewModel => viewModel.IncludeAll,
            viewModel => viewModel.Currencies);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.CurrencyIds = [];
        }
        else
        {
            if (model.Currencies == null || model.Currencies.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Currencies), S["At least one currency is required."]);
            }

            step.IncludeAll = false;
            step.CurrencyIds = model.Currencies?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? [];
        }

        return Edit(step, context);
    }
}
