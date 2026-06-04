using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Display driver that provides the file upload editor shape for importing a local DNC list.
/// </summary>
public sealed class ImportLocalDncListDisplayDriver : DisplayDriver<ImportLocalDncList>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportLocalDncListDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ImportLocalDncListDisplayDriver(IStringLocalizer<ImportLocalDncListDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the editor shape for the import form.
    /// </summary>
    /// <param name="model">The import model.</param>
    /// <param name="context">The build editor context.</param>
    public override Task<IDisplayResult> EditAsync(ImportLocalDncList model, BuildEditorContext context)
        => Task.FromResult<IDisplayResult>(
            Initialize<UploadLocalDncListViewModel>("ImportLocalDncListFile_Edit", viewModel =>
            {
                viewModel.Name = model.Name;
                viewModel.CountryCode = model.CountryCode;
                viewModel.File = model.File;
                viewModel.CountryOptions = GetCountryOptions();
            })
            .Location("Content:1"));

    /// <summary>
    /// Updates the import model from form data.
    /// </summary>
    /// <param name="model">The import model.</param>
    /// <param name="context">The update editor context.</param>
    public override async Task<IDisplayResult> UpdateAsync(ImportLocalDncList model, UpdateEditorContext context)
    {
        var viewModel = new UploadLocalDncListViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            if (string.IsNullOrWhiteSpace(viewModel.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A list name is required."]);
            }

            if (string.IsNullOrWhiteSpace(viewModel.CountryCode))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.CountryCode), S["A country is required."]);
            }

            if (viewModel.File == null || viewModel.File.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["A CSV file is required."]);
            }

            model.Name = viewModel.Name;
            model.CountryCode = viewModel.CountryCode;
            model.File = viewModel.File;
        }

        return await EditAsync(model, context);
    }

    private SelectListItem[] GetCountryOptions()
    {
        return
        [
            new SelectListItem(S["Select a country..."], string.Empty),
            new SelectListItem(S["United States"], "US"),
            new SelectListItem(S["Canada"], "CA"),
            new SelectListItem(S["United Kingdom"], "GB"),
            new SelectListItem(S["Australia"], "AU"),
            new SelectListItem(S["Germany"], "DE"),
            new SelectListItem(S["France"], "FR"),
            new SelectListItem(S["India"], "IN"),
            new SelectListItem(S["Brazil"], "BR"),
            new SelectListItem(S["Mexico"], "MX"),
            new SelectListItem(S["Japan"], "JP"),
            new SelectListItem(S["South Korea"], "KR"),
            new SelectListItem(S["Italy"], "IT"),
            new SelectListItem(S["Spain"], "ES"),
            new SelectListItem(S["Netherlands"], "NL"),
            new SelectListItem(S["Belgium"], "BE"),
            new SelectListItem(S["Switzerland"], "CH"),
            new SelectListItem(S["Sweden"], "SE"),
            new SelectListItem(S["Norway"], "NO"),
            new SelectListItem(S["Denmark"], "DK"),
            new SelectListItem(S["Finland"], "FI"),
            new SelectListItem(S["Ireland"], "IE"),
            new SelectListItem(S["New Zealand"], "NZ"),
            new SelectListItem(S["South Africa"], "ZA"),
            new SelectListItem(S["Argentina"], "AR"),
            new SelectListItem(S["Colombia"], "CO"),
            new SelectListItem(S["Chile"], "CL"),
            new SelectListItem(S["Poland"], "PL"),
            new SelectListItem(S["Austria"], "AT"),
            new SelectListItem(S["Portugal"], "PT"),
        ];
    }
}
