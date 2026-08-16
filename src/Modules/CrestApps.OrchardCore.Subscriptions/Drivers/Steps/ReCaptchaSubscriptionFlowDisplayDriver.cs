using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ReCaptcha.Configuration;
using OrchardCore.ReCaptcha.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

/// <summary>
/// Displays and validates reCAPTCHA on the first subscription flow step when reCAPTCHA is configured.
/// </summary>
public sealed class ReCaptchaSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly ISiteService _siteService;
    private readonly ReCaptchaService _reCaptchaService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReCaptchaSubscriptionFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read reCAPTCHA settings.</param>
    /// <param name="reCaptchaService">The service used to validate submitted reCAPTCHA tokens.</param>
    public ReCaptchaSubscriptionFlowDisplayDriver(
        ISiteService siteService,
        ReCaptchaService reCaptchaService)
    {
        _siteService = siteService;
        _reCaptchaService = reCaptchaService;
    }

    /// <summary>
    /// Builds the reCAPTCHA editor shape for the first subscription flow step when reCAPTCHA settings are complete.
    /// </summary>
    /// <param name="flow">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders reCAPTCHA, or <see langword="null"/> when it should not be shown.</returns>
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

    /// <summary>
    /// Validates reCAPTCHA for the first subscription flow step and rebuilds the reCAPTCHA editor shape.
    /// </summary>
    /// <param name="flow">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result that renders reCAPTCHA, or <see langword="null"/> when validation is not required.</returns>
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
