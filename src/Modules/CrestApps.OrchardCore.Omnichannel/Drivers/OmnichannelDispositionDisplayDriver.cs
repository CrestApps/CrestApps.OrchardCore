using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Drivers;

internal sealed class OmnichannelDispositionDisplayDriver : DisplayDriver<OmnichannelDisposition>
{
    private readonly IStringLocalizer S;

    public OmnichannelDispositionDisplayDriver(
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelDisposition disposition, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelDisposition_Fields_SummaryAdmin", disposition).Location("Content:1"),
            View("OmnichannelDisposition_Buttons_SummaryAdmin", disposition).Location("Actions:5"),
            View("OmnichannelDisposition_DefaultMeta_SummaryAdmin", disposition).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelDisposition disposition, BuildEditorContext context)
    {
        return Initialize<OmnichannelDispositionViewModel>("OmnichannelDispositionFields_Edit", model =>
        {
            model.DisplayText = disposition.DisplayText;
            model.Descriptions = disposition.Descriptions;
            model.CaptureDate = disposition.CaptureDate;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelDisposition disposition, UpdateEditorContext context)
    {
        var model = new OmnichannelDispositionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        model.DisplayText = model.DisplayText.Trim();

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name cannot be empty."]);
        }

        disposition.DisplayText = model.DisplayText;
        disposition.Descriptions = model.Descriptions?.Trim();
        disposition.CaptureDate = model.CaptureDate;

        return Edit(disposition, context);
    }
}
