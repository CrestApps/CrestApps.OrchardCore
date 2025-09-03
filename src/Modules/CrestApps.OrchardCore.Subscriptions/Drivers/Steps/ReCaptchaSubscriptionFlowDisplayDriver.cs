using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ReCaptcha.Configuration;
using OrchardCore.ReCaptcha.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class ReCaptchaSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly ISiteService _siteService;
    private readonly ReCaptchaService _reCaptchaService;

    public ReCaptchaSubscriptionFlowDisplayDriver(
        ISiteService siteService,
        ReCaptchaService reCaptchaService)
    {
        _siteService = siteService;
        _reCaptchaService = reCaptchaService;
    }

    public override async Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (flow.GetCurrentStep() != flow.GetFirstStep())
        {
            return null;
        }

        var _reCaptchaSettings = await _siteService.GetSettingsAsync<ReCaptchaSettings>();

        if (!_reCaptchaSettings.ConfigurationExists())
        {
            return null;
        }

        return View("FormReCaptcha", flow)
            .Location("Content:after");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        if (flow.GetCurrentStep() != flow.GetFirstStep())
        {
            return null;
        }

        var _reCaptchaSettings = await _siteService.GetSettingsAsync<ReCaptchaSettings>();

        if (!_reCaptchaSettings.ConfigurationExists())
        {
            return null;
        }

        await _reCaptchaService.ValidateCaptchaAsync(context.Updater.ModelState.AddModelError);

        return await EditAsync(flow, context);
    }
}
