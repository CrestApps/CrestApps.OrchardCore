using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using CrestApps.OrchardCore.TimeZones.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.TimeZones.Deployments;

internal sealed class TimeZoneMapDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TimeZoneMapDeploymentStep>
{
    private readonly INamedCatalog<TimeZoneMap> _catalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="catalog">The time zone map catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeZoneMapDeploymentStepDisplayDriver(
        INamedCatalog<TimeZoneMap> catalog,
        IStringLocalizer<TimeZoneMapDeploymentStepDisplayDriver> stringLocalizer)
    {
        _catalog = catalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TimeZoneMapDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TimeZoneMapDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TimeZoneMapDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(TimeZoneMapDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<TimeZoneMapDeploymentStepViewModel>("TimeZoneMapDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Maps = (await _catalog.GetAllAsync())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new TimeZoneMapDeploymentStepEntryViewModel
                {
                    ItemId = x.ItemId,
                    Name = x.Name,
                    TimeZoneId = x.TimeZoneId,
                    IsSelected = step.MapIds?.Contains(x.ItemId) ?? false,
                })
                .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(TimeZoneMapDeploymentStep step, UpdateEditorContext context)
    {
        var model = new TimeZoneMapDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Maps);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.MapIds = [];
        }
        else
        {
            if (model.Maps == null || model.Maps.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Maps), S["At least one time zone map is required."]);
            }

            step.IncludeAll = false;
            step.MapIds = model.Maps?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? [];
        }

        return Edit(step, context);
    }
}
