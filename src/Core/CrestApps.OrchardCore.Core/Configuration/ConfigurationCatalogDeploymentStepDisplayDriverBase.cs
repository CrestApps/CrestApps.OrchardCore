using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Provides the editor and summary for a deployment step that exports a group of configuration catalogs.
/// </summary>
/// <typeparam name="TStep">The deployment step type.</typeparam>
public abstract class ConfigurationCatalogDeploymentStepDisplayDriverBase<TStep> : DisplayDriver<DeploymentStep, TStep>
    where TStep : ConfigurationCatalogDeploymentStep, new()
{
    private readonly IEnumerable<IConfigurationCatalog> _catalogs;

    /// <summary>
    /// Gets the localizer used for the catalog labels and validation messages.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCatalogDeploymentStepDisplayDriverBase{TStep}"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ConfigurationCatalogDeploymentStepDisplayDriverBase(
        IEnumerable<IConfigurationCatalog> catalogs,
        IStringLocalizer stringLocalizer)
    {
        _catalogs = catalogs;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the catalog group the step exports.
    /// </summary>
    protected abstract string Group { get; }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(TStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View($"{typeof(TStep).Name}_Summary", step).Location("Summary", "Content"),
            View($"{typeof(TStep).Name}_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(TStep step, BuildEditorContext context)
    {
        return Initialize<ConfigurationCatalogDeploymentStepViewModel>($"{typeof(TStep).Name}_Fields_Edit", model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Catalogs = GetCatalogs()
                .Select(catalog => new ConfigurationCatalogEntryViewModel
                {
                    StepName = catalog.StepName,
                    DisplayText = Describe(catalog.StepName).Value,
                    IsSelected = step.CatalogNames?.Contains(catalog.StepName) ?? false,
                })
                .ToArray();
        }).Location("Content");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TStep step, UpdateEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var model = new ConfigurationCatalogDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Catalogs);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.CatalogNames = [];
        }
        else
        {
            var selected = model.Catalogs?.Where(x => x.IsSelected).Select(x => x.StepName).ToArray() ?? [];

            if (selected.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Catalogs), S["At least one configuration catalog is required."]);
            }

            step.IncludeAll = false;
            step.CatalogNames = selected;
        }

        return Edit(step, context);
    }

    /// <summary>
    /// Gets the text shown to the operator for a catalog.
    /// </summary>
    /// <param name="stepName">The recipe step name of the catalog.</param>
    /// <returns>The label to render, defaulting to the step name.</returns>
    protected virtual LocalizedString Describe(string stepName)
    {
        return new LocalizedString(stepName, stepName);
    }

    private IEnumerable<IConfigurationCatalog> GetCatalogs()
    {
        return _catalogs
            .Where(catalog => string.Equals(catalog.Group, Group, StringComparison.Ordinal))
            .OrderBy(catalog => catalog.Order)
            .ThenBy(catalog => catalog.StepName, StringComparer.Ordinal);
    }
}
