using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Creates or updates an omnichannel address with the fields the address editor and the numbers API set, through the
/// address manager, whose handler validates and canonicalizes it. The settings other features add to the editor are
/// left untouched, and no credential is ever accepted.
/// </summary>
internal sealed class SaveOmnichannelAddressTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveOmnichannelAddress";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the address to update. Leave it out to add an address."
        },
        "addressType": {
          "type": "string",
          "description": "The kind of address, such as PhoneNumber or EmailAddress. Required when adding; it cannot be changed afterwards."
        },
        "displayText": {
          "type": "string",
          "description": "The address's name, such as Support line. Required when adding."
        },
        "value": {
          "type": "string",
          "description": "The address itself: a phone number with its country code (+15550109000) or an email address. Required when adding."
        },
        "capabilities": {
          "type": "array",
          "items": { "type": "string" },
          "description": "What the address is used for, such as Phone and SMS. Replaces the capabilities the address type offers; capabilities of features that are off are kept."
        },
        "providerName": {
          "type": "string",
          "description": "The technical name of the provider that carries the address, such as Telnyx. An empty text clears it."
        },
        "description": {
          "type": "string",
          "description": "A description of the address. An empty text clears it."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates an omnichannel address: display name, address type, value, capabilities (phone, SMS, ...) and provider. Feature-specific settings such as outbound-line agents and messaging line settings are set on the address's admin page. An agent can hold only one number per channel; validation refuses a conflict. Changing an address re-routes its live inbound calls and messages at once. No credential is accepted.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var secret = AIModuleTools.FindSecretArgument(arguments);

        if (secret is not null)
        {
            return $"Nothing was saved: '{secret}' looks like a credential, and a credential is never taken from a chat. A person enters it on the address's admin page.";
        }

        var manager = services.GetRequiredService<ICatalogManager<OmnichannelChannelEndpoint>>();
        var addressOptions = services.GetRequiredService<IOptions<OmnichannelAddressOptions>>().Value;
        var id = AIModuleTools.ReadString(arguments, "id");
        var addressType = AIModuleTools.ReadString(arguments, "addressType");
        OmnichannelChannelEndpoint existing = null;
        OmnichannelChannelEndpoint working;

        if (id is not null)
        {
            existing = await manager.FindByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no omnichannel address has the ID '{id}'. Use listRecords with the kind omnichannelAddress to find it.";
            }

            if (addressType is not null && !string.Equals(addressType, existing.GetAddressType(), StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the address is a {existing.GetAddressType()} and its type cannot be changed. Add a new address instead.";
            }

            working = existing.Clone();
        }
        else
        {
            var creatable = addressOptions.GetCreatableTypes().Select(type => type.Name).ToArray();

            if (addressType is null || !creatable.Contains(addressType, StringComparer.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: addressType is required and must be one of {string.Join(", ", creatable)}.";
            }

            working = await manager.NewAsync(cancellationToken: cancellationToken);
            working.AddressType = addressOptions.AddressTypes[addressType].Name;

            // With a single capability on offer there is nothing to choose, so it starts ticked, as in the editor.
            var offeredCapabilities = addressOptions.GetCapabilities(working.AddressType).ToArray();

            if (offeredCapabilities.Length == 1)
            {
                working.Capabilities = [offeredCapabilities[0].Name];
            }
        }

        if (arguments.TryGetFirstString("displayText", true, out var displayText))
        {
            working.DisplayText = displayText?.Trim();
        }

        if (arguments.TryGetFirstString("value", true, out var value))
        {
            working.Value = value?.Trim();
        }

        if (arguments.TryGetFirstString("description", true, out var description))
        {
            working.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        if (arguments.TryGetFirstString("providerName", true, out var providerName))
        {
            working.ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName.Trim();
        }

        var capabilities = OmnichannelToolSupport.ReadStrings(arguments, "capabilities");

        if (capabilities is not null)
        {
            var offered = addressOptions.GetCapabilities(working.GetAddressType()).Select(capability => capability.Name).ToArray();
            var unknown = capabilities.Where(capability => !offered.Contains(capability, StringComparer.OrdinalIgnoreCase)).ToArray();

            if (unknown.Length > 0)
            {
                return $"Nothing was saved: {string.Join(", ", unknown)} cannot be used on a {working.GetAddressType()}. Use {string.Join(", ", offered)}.";
            }

            working.Capabilities = OmnichannelAddressCapabilities.Merge(addressOptions, working, capabilities);
        }

        var validation = await manager.ValidateAsync(working, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + OmnichannelToolSupport.Reasons(validation);
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
        }
        else
        {
            existing.DisplayText = working.DisplayText;
            existing.Value = working.Value;
            existing.Description = working.Description;
            existing.ProviderName = working.ProviderName;
            existing.Capabilities = working.Capabilities;

            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        var saved = existing ?? working;

        return AIModuleTools.Json(new
        {
            saved = existing is null ? "created" : "updated",
            address = await OmnichannelAddressToolSupport.DescribeAsync(services, saved, full: true),
        });
    }
}
