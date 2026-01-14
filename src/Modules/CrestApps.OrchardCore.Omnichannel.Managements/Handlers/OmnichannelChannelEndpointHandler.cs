using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelChannelEndpointHandler : CatalogEntryHandlerBase<OmnichannelChannelEndpoint>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly IPhoneFormatValidator _phoneFormatValidator;
    private readonly IEmailAddressValidator _emailAddressValidator;

    internal readonly IStringLocalizer S;

    public OmnichannelChannelEndpointHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IPhoneFormatValidator phoneFormatValidator,
        IEmailAddressValidator emailAddressValidator,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _phoneFormatValidator = phoneFormatValidator;
        _emailAddressValidator = emailAddressValidator;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelChannelEndpoint> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelChannelEndpoint> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelChannelEndpoint> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelChannelEndpoint.DisplayText)]));
        }

        var hasValue = !string.IsNullOrWhiteSpace(context.Model.Value);

        if (!hasValue)
        {
            context.Result.Fail(new ValidationResult(S["Endpoint Value is required."], [nameof(OmnichannelChannelEndpoint.Value)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Channel))
        {
            context.Result.Fail(new ValidationResult(S["Channel is required."], [nameof(OmnichannelChannelEndpoint.Channel)]));
        }
        else if (hasValue)
        {
            if (context.Model.Channel == OmnichannelConstants.Channels.Phone || context.Model.Channel == OmnichannelConstants.Channels.Sms)
            {
                if (!_phoneFormatValidator.IsValid(context.Model.Value))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid phone number. Please enter a valid international number in the format: +<CountryCode><Number> (e.g., +14155552671)."], [nameof(OmnichannelChannelEndpoint.Value)]));
                }
            }
            else if (context.Model.Channel == OmnichannelConstants.Channels.Email)
            {
                if (!_emailAddressValidator.Validate(context.Model.Value))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid email address."], [nameof(OmnichannelChannelEndpoint.Value)]));
                }
            }
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelChannelEndpoint> context)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(OmnichannelChannelEndpoint enabpoint, JsonNode data)
    {
        var displayText = data[nameof(OmnichannelCampaign.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            enabpoint.DisplayText = displayText;
        }

        var descriptionText = data[nameof(OmnichannelChannelEndpoint.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(descriptionText))
        {
            enabpoint.Description = descriptionText;
        }

        var channelText = data[nameof(OmnichannelChannelEndpoint.Channel)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(channelText))
        {
            enabpoint.Channel = channelText;
        }

        var valueText = data[nameof(OmnichannelChannelEndpoint.Value)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(valueText))
        {
            enabpoint.Value = valueText;
        }

        var properties = data[nameof(OmnichannelCampaign.Properties)]?.AsObject();

        if (properties != null)
        {
            enabpoint.Properties ??= [];
            enabpoint.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
