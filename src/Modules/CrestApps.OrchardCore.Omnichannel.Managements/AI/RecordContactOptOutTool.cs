using System.Net.Mail;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Records that a person asked not to be contacted on a channel at an address, through <see cref="IConsentService"/>,
/// the one place every STOP keyword and unsubscribe link records an opt-out. With a contact, the contact's do-not-call,
/// do-not-text or do-not-e-mail flag is set (as the contact editor sets it); without one, the opt-out is kept against the
/// address. Every send, dial and campaign asks the consent service, so the address is skipped from then on. The tool
/// only records opt-outs: opting someone back in is left to the person (a START keyword) or the contact editor.
/// </summary>
internal sealed class RecordContactOptOutTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "recordContactOptOut";

    /// <summary>
    /// The source kept with an opt-out this tool records.
    /// </summary>
    public const string Source = "AIAssistant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "channel": {
          "type": "string",
          "enum": ["phone", "sms", "email"],
          "description": "The channel the person asked not to be contacted on: phone for calls, sms for text messages, email for e-mail."
        },
        "address": {
          "type": "string",
          "description": "The phone number in E.164 form, such as +15551234567, or the e-mail address."
        },
        "contactId": {
          "type": "string",
          "description": "The content item ID of the CRM contact who asked, when known. Their do-not-call, do-not-text or do-not-e-mail flag is set."
        },
        "reason": {
          "type": "string",
          "description": "Optional short note on how the request came in, for example asked on a call. Kept with the opt-out."
        }
      },
      "required": ["channel", "address"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records that a person asked not to be contacted on a channel (phone calls, text messages or e-mail) at a phone number or e-mail address, for example when a customer asks to stop. With a contactId the contact's do-not-call, do-not-text or do-not-e-mail flag is set; without one the opt-out is kept against the address. Opted-out numbers and addresses are skipped by every send, dial and campaign from then on. It only records opt-outs; it never opts anyone back in.";

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

        // Recording an opt-out stops the site contacting someone, the safe direction, so it takes the permission that
        // lets a person create and edit activities (the one startAIActivity checks), and the contact's own edit
        // permission when a contact is named.
        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.EditActivity))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var channel = AIModuleTools.ReadString(arguments, "channel")?.ToLowerInvariant() switch
        {
            "phone" or "call" => ConsentChannels.Phone,
            "sms" or "text" => ConsentChannels.Sms,
            "email" => ConsentChannels.Email,
            _ => null,
        };

        if (channel is null)
        {
            return "Nothing was recorded: channel must be phone, sms or email.";
        }

        var address = AIModuleTools.ReadString(arguments, "address")?.Trim();

        if (channel == ConsentChannels.Email)
        {
            if (address is null || !MailAddress.TryCreate(address, out var mailAddress) || !string.Equals(mailAddress.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                return "Nothing was recorded: give the e-mail address, such as jane@example.com.";
            }
        }
        else if (!AIActivitiesApiEndpoints.IsPhoneNumber(address))
        {
            return "Nothing was recorded: give the phone number in E.164 form, such as +15551234567.";
        }

        var contactId = AIModuleTools.ReadString(arguments, "contactId");

        if (contactId is not null)
        {
            var contact = await services.GetRequiredService<IContentManager>().GetAsync(contactId, VersionOptions.Latest);

            if (contact is null || !contact.Has<OmnichannelContactPart>())
            {
                return $"Nothing was recorded: no CRM contact has the ID '{contactId}'. Leave contactId out to record the opt-out against the address.";
            }

            if (!await AIModuleTools.IsAuthorizedAsync(services, CommonPermissions.EditContent, contact))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        var reason = AIModuleTools.ReadString(arguments, "reason")?.Trim();

        if (reason is { Length: > 200 })
        {
            reason = reason[..200];
        }

        await services.GetRequiredService<IConsentService>().RecordAsync(channel, address, new ConsentChange
        {
            OptedOut = true,
            Source = string.IsNullOrEmpty(reason) ? Source : $"{Source}: {reason}",
            ContactKind = contactId is null ? null : ConsentContactKinds.Contact,
            ContactId = contactId,
        }, cancellationToken);

        var how = channel switch
        {
            ConsentChannels.Sms => "text messages",
            ConsentChannels.Email => "e-mail",
            _ => "phone calls",
        };

        return AIModuleTools.Json(new
        {
            recorded = true,
            channel,
            address,
            contactId,
            message = contactId is null
                ? $"{address} is now opted out of {how}. Opted-out numbers and addresses are skipped by every send, dial and campaign."
                : $"The contact is now opted out of {how} at {address}. Opted-out numbers and addresses are skipped by every send, dial and campaign; the flag can be cleared on the contact's page.",
        });
    }
}
