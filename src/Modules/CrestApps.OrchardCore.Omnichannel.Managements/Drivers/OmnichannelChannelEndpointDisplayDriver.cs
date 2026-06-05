using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Email;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelChannelEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IEmailAddressValidator _emailAddressValidator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDisplayDriver"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="emailAddressValidator">The email address validator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointDisplayDriver(
        IPhoneNumberService phoneNumberService,
        IEmailAddressValidator emailAddressValidator,
        IStringLocalizer<OmnichannelChannelEndpointDisplayDriver> stringLocalizer)
    {
        _phoneNumberService = phoneNumberService;
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

        var value = model.Value.Trim();

        if (string.IsNullOrWhiteSpace(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel is a required field."]);
        }
        else if (hasValue)
        {
            if (model.Channel == OmnichannelConstants.Channels.Phone || model.Channel == OmnichannelConstants.Channels.Sms)
            {
                if (!_phoneNumberService.TryFormatToE164(model.Value, null, out var e164Number))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Value), S["Invalid phone number. Please enter a valid international number in the format: +<CountryCode><Number> (e.g., +14155552671)."]);
                }
                else
                {
                    value = e164Number;
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
        endpoint.Value = value;

        return Edit(endpoint, context);
    }
}
