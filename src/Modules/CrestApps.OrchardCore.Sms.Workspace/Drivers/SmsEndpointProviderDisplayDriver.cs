using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Sms.Workspace.Drivers;

/// <summary>
/// Adds the provider selector to an SMS channel endpoint: a dropdown of the SMS providers that are currently
/// enabled on the tenant, written to the endpoint's <see cref="OmnichannelChannelEndpoint.ProviderName"/> that
/// the dispatcher uses to route outbound messages through the provider that owns the number.
/// </summary>
public sealed class SmsEndpointProviderDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IOptionsMonitor<SmsProviderOptions> _smsProviderOptions;

    public SmsEndpointProviderDisplayDriver(IOptionsMonitor<SmsProviderOptions> smsProviderOptions)
    {
        _smsProviderOptions = smsProviderOptions;
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return null;
        }

        return Initialize<SmsEndpointProviderViewModel>("SmsEndpointProvider_Edit", model =>
        {
            model.ProviderName = endpoint.ProviderName;
            model.Providers = _smsProviderOptions.CurrentValue.Providers
                .Where(entry => entry.Value.IsEnabled)
                .Select(entry => new SelectListItem(entry.Key, entry.Key))
                .OrderBy(item => item.Text)
                .ToArray();
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new SmsEndpointProviderViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        endpoint.ProviderName = model.ProviderName?.Trim();

        return Edit(endpoint, context);
    }

    private static bool IsSms(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);
}
