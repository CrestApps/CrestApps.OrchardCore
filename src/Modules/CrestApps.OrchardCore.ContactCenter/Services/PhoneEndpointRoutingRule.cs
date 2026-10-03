using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Refuses a phone number whose inbound routing names an entry point that does not exist, whether it is saved from the
/// editor or imported by a recipe, so calls to it never go to a door that is not there.
/// </summary>
internal sealed class PhoneEndpointRoutingRule : IChannelEndpointRule
{
    private readonly IContactCenterEntryPointManager _entryPointManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneEndpointRoutingRule"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneEndpointRoutingRule(
        IContactCenterEntryPointManager entryPointManager,
        IStringLocalizer<PhoneEndpointRoutingRule> stringLocalizer)
    {
        _entryPointManager = entryPointManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        var endpoint = context.Model;

        if (!string.Equals(endpoint?.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var entryPointId = endpoint.GetOrCreate<PhoneEndpointRoutingSettings>().EntryPointId;

        if (string.IsNullOrEmpty(entryPointId) ||
            await _entryPointManager.FindByIdAsync(entryPointId, cancellationToken) is not null)
        {
            return;
        }

        context.Result.Fail(new ValidationResult(
            S["The selected entry point no longer exists."],
            [nameof(PhoneEndpointRoutingSettings.EntryPointId)]));
    }
}
