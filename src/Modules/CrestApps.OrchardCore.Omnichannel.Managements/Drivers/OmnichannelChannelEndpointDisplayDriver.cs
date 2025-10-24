using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Email;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelChannelEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IPhoneFormatValidator _phoneFormatValidator;
    private readonly IEmailAddressValidator _emailAddressValidator;

    private readonly IStringLocalizer S;

    public OmnichannelChannelEndpointDisplayDriver(
        IPhoneFormatValidator phoneFormatValidator,
        IEmailAddressValidator emailAddressValidator,
        IStringLocalizer<OmnichannelChannelEndpointDisplayDriver> stringLocalizer)
    {
        _phoneFormatValidator = phoneFormatValidator;
        _emailAddressValidator = emailAddressValidator;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelChannelEndpoint endpoint, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelChannelEndpoint_Fields_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelChannelEndpoint_Buttons_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelChannelEndpoint_DefaultMeta_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        return Initialize<OmnichannelChannelEndpointViewModel>("OmnichannelChannelEndpointFields_Edit", model =>
        {
            model.DisplayText = endpoint.DisplayText;
            model.Description = endpoint.Description;
            model.Channel = endpoint.Channel;
            model.Value = endpoint.Value;
            model.Channels =
            [
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
                new(S["Email"], OmnichannelConstants.Channels.Email),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        var model = new OmnichannelChannelEndpointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name is a required field."]);
        }

        var hasValue = !string.IsNullOrWhiteSpace(model.Value);

        if (!hasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["Endpoint value is a required field."]);
        }

        if (string.IsNullOrWhiteSpace(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel is a required field."]);
        }
        else if (hasValue)
        {
            if (model.Channel == OmnichannelConstants.Channels.Phone || model.Channel == OmnichannelConstants.Channels.Sms)
            {
                if (!_phoneFormatValidator.IsValid(model.Value))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["Invalid phone number. Please enter a valid international number in the format: +<CountryCode><Number> (e.g., +14155552671)."]);
                }
            }
            else if (model.Channel == OmnichannelConstants.Channels.Email)
            {
                if (!_emailAddressValidator.Validate(model.Value))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["Invalid email address."]);
                }
            }
        }

        endpoint.DisplayText = model.DisplayText?.Trim();
        endpoint.Description = model.Description?.Trim();
        endpoint.Channel = model.Channel;
        endpoint.Value = model.Value?.Trim();

        return Edit(endpoint, context);
    }
}
