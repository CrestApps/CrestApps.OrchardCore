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
/// Tells whether a person opted out of a channel at a number or address, by asking <see cref="IConsentService"/>, the
/// owner of "may we reach them?" that every send, dial and campaign asks: the contact's do-not-call, do-not-text and
/// do-not-e-mail flags (followed to every record that shares the number), the opt-outs recorded against the address and
/// the site's do-not-call lists. The tool never reads a do-not-call list or a contact flag itself. Consent records only
/// opt-outs, so a person may be contacted unless one is found.
/// </summary>
internal sealed class CheckContactConsentTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "checkContactConsent";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "channel": {
          "type": "string",
          "enum": ["phone", "sms", "email"],
          "description": "The channel to check: phone for calls, sms for text messages, email for e-mail."
        },
        "address": {
          "type": "string",
          "description": "The phone number in E.164 form, such as +15551234567, or the e-mail address."
        },
        "contactId": {
          "type": "string",
          "description": "The content item ID of the CRM contact the person is, when known, so the contact's own do-not-call, do-not-text or do-not-e-mail flag is checked too."
        }
      },
      "required": ["channel", "address"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Tells whether a person opted out of phone calls, text messages or e-mail at a number or address, optionally for a CRM contact, and what recorded the opt-out (the contact's flags, an opt-out recorded against the address, or a do-not-call list). It asks the same consent service every send, dial and campaign asks; opted-out numbers and addresses are skipped. It does not check quiet hours, calling windows or numbers not in service.";

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

        // Someone who works activities (lists them, or creates and edits them, the permission recordContactOptOut and
        // startAIActivity check) may ask whether a person can be reached.
        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ListActivities) &&
            !await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.EditActivity))
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
            return "channel must be phone, sms or email.";
        }

        var address = AIModuleTools.ReadString(arguments, "address")?.Trim();

        if (channel == ConsentChannels.Email)
        {
            if (address is null || !MailAddress.TryCreate(address, out var mailAddress) || !string.Equals(mailAddress.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                return "Give the e-mail address, such as jane@example.com.";
            }
        }
        else if (!AIActivitiesApiEndpoints.IsPhoneNumber(address))
        {
            return "Give the phone number in E.164 form, such as +15551234567.";
        }

        var contactId = AIModuleTools.ReadString(arguments, "contactId");
        ContentItem contact = null;

        if (contactId is not null)
        {
            contact = await services.GetRequiredService<IContentManager>().GetAsync(contactId, VersionOptions.Latest);

            if (contact is null || !contact.Has<OmnichannelContactPart>())
            {
                return $"No CRM contact has the ID '{contactId}'. Leave contactId out to check the address alone.";
            }

            if (!await AIModuleTools.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contact))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        var consent = services.GetService<IConsentService>();

        if (consent is null)
        {
            return "Consent cannot be checked on this site: no consent service is registered.";
        }

        var status = await consent.GetAsync(new ConsentQuery
        {
            Channel = channel,
            Address = address,
            ContactKind = contact is null ? null : ConsentContactKinds.Contact,
            ContactId = contact?.ContentItemId,
            Contact = contact,
        }, cancellationToken);

        var how = channel switch
        {
            ConsentChannels.Sms => "text messages",
            ConsentChannels.Email => "e-mail",
            _ => "phone calls",
        };

        return AIModuleTools.Json(new
        {
            channel,
            address,
            contactId = contact?.ContentItemId,
            optedOut = status.HasOptedOut,
            recordedBy = status.HasOptedOut ? DescribeSource(status.Source) : null,
            message = status.HasOptedOut
                ? $"{address} has opted out of {how}. Opted-out numbers and addresses are skipped by every send, dial and campaign."
                : $"{address} has not opted out of {how}. Quiet hours, calling windows and numbers not in service are checked separately, when a call or message is placed.",
        });
    }

    private static string DescribeSource(string source)
        => source switch
        {
            ConsentSourceNames.ContactPreferences => "the contact's communication preferences (do-not-call, do-not-text or do-not-e-mail)",
            ConsentSourceNames.DoNotCallLists => "one of the site's do-not-call lists",
            ConsentSourceNames.CustomerMarketing => "the customer's marketing preferences",
            _ => $"an opt-out recorded against the address ({source})",
        };
}
