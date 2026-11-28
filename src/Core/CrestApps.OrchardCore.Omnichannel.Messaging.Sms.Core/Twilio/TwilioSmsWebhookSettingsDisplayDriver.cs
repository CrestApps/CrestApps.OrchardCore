using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;

/// <summary>
/// Shows the inbound-SMS webhook address inside Orchard Core's Twilio tab on the SMS settings screen, so the operator
/// can copy it into the Twilio console. It only displays; Orchard Core's own Twilio driver owns and saves the settings,
/// which is why this is not a driver of the Twilio settings section.
/// </summary>
internal sealed class TwilioSmsWebhookSettingsDisplayDriver : DisplayDriver<ISite>
{
    // Matches Orchard Core's SMS settings group, so the address shows under /Admin/Settings/sms.
    private const string SmsSettingsGroupId = "sms";

    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public TwilioSmsWebhookSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TwilioSmsWebhookSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, BuildEditorContext context)
    {
        return Initialize<TwilioSmsWebhookSettingsViewModel>("TwilioSmsWebhookSettings_Edit", model =>
            {
                model.WebhookUrl = BuildWebhookUrl(site);
            })
            // Orchard Core places its Twilio editor at Content:5 in the Twilio provider card of the Twilio tab. This comes
            // before it, in the same card, and the view then moves the address to the top of that editor's collapsible
            // settings, as the Telnyx settings show it.
            .Location(l => l.Zone("Content", "4").Tab(S["Twilio"]).Card(S["Twilio Provider"], "10"))
            .OnGroup(SmsSettingsGroupId);
    }

    // The tenant's site Base URL wins because Twilio signs the exact address it calls, and the webhook checks the
    // signature against that same Base URL. Without one, the current request stands in as a best guess.
    private string BuildWebhookUrl(ISite site)
    {
        var baseUrl = site.BaseUrl?.Trim();

        if (string.IsNullOrEmpty(baseUrl))
        {
            var request = _httpContextAccessor.HttpContext?.Request;

            if (request is not null && request.Host.HasValue)
            {
                baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            }
        }

        return string.IsNullOrEmpty(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}/{TwilioWebhookEndpoint.SmsWebhookPath}";
    }
}
