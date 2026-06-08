using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelDispositionDisplayDriver : DisplayDriver<OmnichannelDisposition>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelDispositionDisplayDriver(
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelDisposition disposition, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelDisposition_Fields_SummaryAdmin", disposition)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
        View("OmnichannelDisposition_Buttons_SummaryAdmin", disposition)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
        View("OmnichannelDisposition_DefaultMeta_SummaryAdmin", disposition)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelDisposition disposition, BuildEditorContext context)
    {
        return Initialize<OmnichannelDispositionViewModel>("OmnichannelDispositionFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = disposition.Name;
            model.Description = disposition.Description;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelDisposition disposition, UpdateEditorContext context)
    {
        var model = new OmnichannelDispositionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        var name = model.Name?.Trim();

        if (context.IsNew)
        {
            disposition.Name = name;
        }

        disposition.Description = model.Description?.Trim();

        return Edit(disposition, context);
    }
}
