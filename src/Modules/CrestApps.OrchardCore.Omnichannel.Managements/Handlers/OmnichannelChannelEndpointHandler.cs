using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Email;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelChannelEndpointHandler : CatalogEntryHandlerBase<OmnichannelChannelEndpoint>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly IEnumerable<IChannelEndpointAddressPolicy> _addressPolicies;
    private readonly IOmnichannelChannelEndpointStore _store;
    private readonly IEnumerable<IChannelEndpointRule> _rules = [];

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="emailAddressValidator">The email address validator.</param>
    /// <param name="addressPolicies">The address rules other features contribute for the channels they add.</param>
    /// <param name="store">The address store, read directly because the catalog manager runs this handler.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IPhoneNumberService phoneNumberService,
        IEmailAddressValidator emailAddressValidator,
        IEnumerable<IChannelEndpointAddressPolicy> addressPolicies,
        IOmnichannelChannelEndpointStore store,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _phoneNumberService = phoneNumberService;
        _emailAddressValidator = emailAddressValidator;
        _addressPolicies = addressPolicies;
        _store = store;
        S = stringLocalizer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointHandler"/> class that also runs the rules
    /// other features add to the endpoints they extend.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="emailAddressValidator">The email address validator.</param>
    /// <param name="addressPolicies">The address rules other features contribute for the channels they add.</param>
    /// <param name="store">The address store, read directly because the catalog manager runs this handler.</param>
    /// <param name="rules">The endpoint rules other features contribute.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IPhoneNumberService phoneNumberService,
        IEmailAddressValidator emailAddressValidator,
        IEnumerable<IChannelEndpointAddressPolicy> addressPolicies,
        IOmnichannelChannelEndpointStore store,
        IEnumerable<IChannelEndpointRule> rules,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
        : this(httpContextAccessor, clock, phoneNumberService, emailAddressValidator, addressPolicies, store, stringLocalizer)
    {
        _rules = rules;
    }

    public override async Task InitializingAsync(InitializingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        await PopulateAsync(context.Model, context.Data);

        Canonicalize(context.Model);
    }

    public override async Task UpdatingAsync(UpdatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        await PopulateAsync(context.Model, context.Data);

        Canonicalize(context.Model);
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        // An editor builds a new endpoint before it binds the form to it, so the value is still empty when the entry is
        // initialized and only the create raises after the form has been bound.
        Canonicalize(context.Model);

        return Task.CompletedTask;
    }

    private void Canonicalize(OmnichannelChannelEndpoint endpoint)
    {
        BringForward(endpoint);

        // An address is matched against inbound traffic by its value, so the value a caller is recognised by has to be
        // the same however it was written. Canonicalizing only in the editor left a recipe or an import storing
        // whatever it was given, and an address that never matched anything.
        if (string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return;
        }

        endpoint.Value = endpoint.Value.Trim();

        if (endpoint.AddressType == OmnichannelAddressTypes.PhoneNumber)
        {
            if (_phoneNumberService.TryParse(endpoint.Value, null, out var canonicalNumber))
            {
                endpoint.Value = canonicalNumber.Value;
            }

            return;
        }

        if (endpoint.AddressType == OmnichannelAddressTypes.EmailAddress)
        {
            // Inbound mail names the address in whatever case the sender typed, so it is stored in the one form it is
            // matched in.
            endpoint.Value = OmnichannelEmailAddress.Normalize(endpoint.Value) ?? endpoint.Value;

            return;
        }

        // An address type another feature contributes (a messaging channel such as WhatsApp) says how its addresses
        // are stored, so its addresses match their inbound traffic the way phone numbers do.
        var policy = FindPolicy(endpoint);

        if (policy is not null)
        {
            var normalized = policy.Normalize(PolicyChannel(endpoint, policy), endpoint.Value);

            if (!string.IsNullOrEmpty(normalized))
            {
                endpoint.Value = normalized;
            }
        }
    }

    // A record saved, imported or exported before addresses had a type and capabilities carries a single channel. It is
    // given the type and the capability that channel meant, so it reads the same as an address saved today.
    private static void BringForward(OmnichannelChannelEndpoint endpoint)
    {
        if (string.IsNullOrEmpty(endpoint.AddressType))
        {
            endpoint.AddressType = endpoint.GetAddressType();
        }

        if (endpoint.Capabilities is not { Count: > 0 })
        {
            endpoint.Capabilities = [.. endpoint.GetCapabilities()];
        }
    }

    private IChannelEndpointAddressPolicy FindPolicy(OmnichannelChannelEndpoint endpoint)
        => _addressPolicies.FirstOrDefault(candidate =>
            candidate.AppliesTo(endpoint.AddressType) || endpoint.GetCapabilities().Any(candidate.AppliesTo));

    private static string PolicyChannel(OmnichannelChannelEndpoint endpoint, IChannelEndpointAddressPolicy policy)
        => policy.AppliesTo(endpoint.AddressType)
            ? endpoint.AddressType
            : endpoint.GetCapabilities().First(policy.AppliesTo);

    public override async Task ValidatingAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
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

        BringForward(context.Model);

        if (string.IsNullOrWhiteSpace(context.Model.AddressType))
        {
            context.Result.Fail(new ValidationResult(S["Choose the kind of address."], [nameof(OmnichannelChannelEndpoint.AddressType)]));
        }
        else if (hasValue)
        {
            if (context.Model.AddressType == OmnichannelAddressTypes.PhoneNumber)
            {
                if (!_phoneNumberService.TryParse(context.Model.Value, null, out _))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid phone number. Please enter a valid international number in the format: +<CountryCode><Number> (e.g., +14155552671)."], [nameof(OmnichannelChannelEndpoint.Value)]));
                }
            }
            else if (context.Model.AddressType == OmnichannelAddressTypes.EmailAddress)
            {
                if (!_emailAddressValidator.Validate(context.Model.Value))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid email address."], [nameof(OmnichannelChannelEndpoint.Value)]));
                }
            }
            else if (FindPolicy(context.Model) is { } policy &&
                policy.Validate(PolicyChannel(context.Model, policy), context.Model.Value) is { } error)
            {
                context.Result.Fail(new ValidationResult(error, [nameof(OmnichannelChannelEndpoint.Value)]));
            }
        }

        if (context.Model.GetCapabilities().Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Choose what this address is used for."], [nameof(OmnichannelChannelEndpoint.Capabilities)]));
        }

        // An address is listed once, with every capability it has. Two records for one number made inbound traffic
        // match whichever was found first, and its settings depended on luck.
        if (hasValue && !string.IsNullOrWhiteSpace(context.Model.AddressType))
        {
            // A new address is validated before it is canonicalized on create, so the comparison canonicalizes it too.
            var value = context.Model.Value.Trim();

            if (context.Model.AddressType == OmnichannelAddressTypes.PhoneNumber &&
                _phoneNumberService.TryParse(value, null, out var canonicalNumber))
            {
                value = canonicalNumber.Value;
            }
            else if (context.Model.AddressType == OmnichannelAddressTypes.EmailAddress)
            {
                value = OmnichannelEmailAddress.Normalize(value) ?? value;
            }

            var addresses = await _store.GetAllAsync(cancellationToken);
            var duplicate = addresses.FirstOrDefault(other =>
                !string.Equals(other.ItemId, context.Model.ItemId, StringComparison.Ordinal) &&
                string.Equals(other.GetAddressType(), context.Model.AddressType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(other.Value, value, StringComparison.OrdinalIgnoreCase));

            if (duplicate is not null)
            {
                context.Result.Fail(new ValidationResult(
                    S["{0} is already in the address list as '{1}'. Tick the extra capability on that address instead.", context.Model.Value, duplicate.DisplayText],
                    [nameof(OmnichannelChannelEndpoint.Value)]));
            }
        }

        // The rules of the features that extend endpoints, such as the agents a phone number's outbound line names.
        foreach (var rule in _rules)
        {
            await rule.ValidateAsync(context, cancellationToken);
        }
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
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

    private Task PopulateAsync(OmnichannelChannelEndpoint enabpoint, JsonNode data)
    {
        OmnichannelDeploymentSerializer.Populate(enabpoint, data);

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

        BringForward(enabpoint);

        var valueText = data[nameof(OmnichannelChannelEndpoint.Value)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(valueText))
        {
            enabpoint.Value = NormalizePhoneValue(enabpoint.AddressType, valueText);
        }

        var properties = data[nameof(OmnichannelCampaign.Properties)]?.AsObject();

        if (properties != null)
        {
            enabpoint.Properties ??= new Dictionary<string, object>();
            foreach (var (key, value) in properties)
            {
                enabpoint.Properties[key] = value;
            }
        }

        return Task.CompletedTask;
    }

    private string NormalizePhoneValue(string addressType, string value)
    {
        if (addressType != OmnichannelAddressTypes.PhoneNumber ||
            !_phoneNumberService.TryParse(value, null, out var canonicalNumber))
        {
            return value;
        }

        return canonicalNumber.Value;
    }
}
