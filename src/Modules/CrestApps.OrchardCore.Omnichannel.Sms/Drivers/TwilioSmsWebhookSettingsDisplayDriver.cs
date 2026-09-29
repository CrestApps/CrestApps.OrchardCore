using CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Sms.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Drivers;

/// <summary>
/// Shows the inbound-SMS webhook address inside Orchard Core's Twilio tab on the SMS settings screen, so the operator
/// can copy it into the Twilio console. It only displays; Orchard Core's own Twilio driver owns and saves the settings,
/// which is why this is not a driver of the Twilio settings section.
/// </summary>
public sealed class TwilioSmsWebhookSettingsDisplayDriver : DisplayDriver<ISite>
{
    // Matches Orchard Core's SMS settings group, so the address shows under /Admin/Settings/sms.
    private const string SmsSettingsGroupId = "sms";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TwilioSmsWebhookSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, BuildEditorContext context)
    {
        return Initialize<TwilioSmsWebhookSettingsViewModel>("TwilioSmsWebhookSettings_Edit", model =>
            {
                model.WebhookUrl = BuildWebhookUrl(site);
            })
            // Orchard Core places its Twilio editor at Content:5 in the Twilio tab. This comes before it, and the view
            // then moves the address to the top of that editor's collapsible settings, as the Telnyx settings show it.
            .Location("Content:4#Twilio")
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
