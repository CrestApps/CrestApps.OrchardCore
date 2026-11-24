using System.Text.Json;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Queues an automated AI call or text to one phone number through <see cref="AIActivityStarter"/>, the service the AI
/// activities API and the workflow task use. The activity is queued: the automated activities processor screens it
/// when it is placed (the contact's opt-out, the do-not-call registries and calling window, numbers not in service),
/// exactly as it screens every automated activity. The tool also refuses up front what it can tell already: it asks
/// <see cref="IConsentService"/> whether the number opted out, and checks the numbers not in service.
/// </summary>
internal sealed class StartAIActivityTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "startAIActivity";

    /// <summary>
    /// The source recorded on an activity this tool starts, so it can be told apart from one the API or a workflow started.
    /// </summary>
    public const string Source = "AIAssistant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "channel": {
          "type": "string",
          "enum": ["call", "text"],
          "description": "call for an AI phone call, text for an AI text conversation where the AI sends the first message."
        },
        "to": {
          "type": "string",
          "description": "The phone number to call or text, in E.164 form such as +15551234567."
        },
        "aiProfileId": {
          "type": "string",
          "description": "The ID of the AI profile that holds the conversation."
        },
        "fromAddressId": {
          "type": "string",
          "description": "The ID of the omnichannel address to call or text from. Leave it out to use the default for the channel."
        },
        "contactId": {
          "type": "string",
          "description": "The content item ID of the contact the call or text is for."
        },
        "campaignId": {
          "type": "string",
          "description": "The ID of the campaign the activity belongs to."
        },
        "subjectContentType": {
          "type": "string",
          "description": "The technical name of the subject of the activity; it must be configured under Subject Flows."
        },
        "scheduledAt": {
          "type": "string",
          "description": "When to place it, as a date and time in the user's time zone (or with an offset). Leave it out to place it as soon as possible."
        }
      },
      "required": ["channel", "to", "aiProfileId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues an automated AI phone call or AI text conversation to one phone number (E.164) with a given AI profile, optionally from a chosen address, for a contact, campaign and subject, now or at a scheduled time. Contacts a real customer and uses paid telephony and model usage. An opted-out number is refused up front. The tool queues the activity; it does not place the call or send the text itself: the queued activity is screened again for opt-out, do-not-call, the calling window and numbers not in service when it is placed.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.EditActivity))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var channel = AIModuleTools.ReadString(arguments, "channel")?.ToLowerInvariant() switch
        {
            "call" or "phone" => OmnichannelConstants.Channels.Phone,
            "text" or "sms" => OmnichannelConstants.Channels.Sms,
            _ => null,
        };

        if (channel is null)
        {
            return "Nothing was queued: channel must be call or text.";
        }

        var to = AIModuleTools.ReadString(arguments, "to");

        if (!AIActivitiesApiEndpoints.IsPhoneNumber(to))
        {
            return "Nothing was queued: give the phone number in E.164 form, such as +15551234567.";
        }

        var profileId = AIModuleTools.ReadString(arguments, "aiProfileId");

        if (profileId is null || await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(profileId, cancellationToken) is null)
        {
            return "Nothing was queued: name an AI profile that exists in aiProfileId.";
        }

        var (validTime, scheduledUtc) = await OmnichannelToolSupport.TryReadLocalDateTimeAsync(services, arguments, "scheduledAt");

        if (!validTime)
        {
            return "Nothing was queued: scheduledAt is not a date and time.";
        }

        var fromAddressId = AIModuleTools.ReadString(arguments, "fromAddressId");

        if (fromAddressId is not null)
        {
            var endpoint = await services.GetRequiredService<ICatalogManager<OmnichannelChannelEndpoint>>().FindByIdAsync(fromAddressId, cancellationToken);

            if (endpoint is null || !endpoint.HasCapability(channel))
            {
                return $"Nothing was queued: no omnichannel address with the ID '{fromAddressId}' can be used for {(channel == OmnichannelConstants.Channels.Sms ? "text messages" : "phone calls")}. Use listRecords with the kind omnichannelAddress to find one.";
            }
        }

        var campaignId = AIModuleTools.ReadString(arguments, "campaignId");

        if (campaignId is not null && await services.GetRequiredService<ICatalog<OmnichannelCampaign>>().FindByIdAsync(campaignId, cancellationToken) is null)
        {
            return $"Nothing was queued: no campaign has the ID '{campaignId}'. Use listRecords with the kind omnichannelCampaign to find it.";
        }

        var subject = AIModuleTools.ReadString(arguments, "subjectContentType");

        if (subject is not null && await services.GetRequiredService<ISubjectFlowSettingsService>().FindConfiguredFlowSettingsAsync(subject, cancellationToken) is null)
        {
            return $"Nothing was queued: the subject '{subject}' is not configured under Subject Flows. Call listSubjectFlows to see the subjects.";
        }

        var contactId = AIModuleTools.ReadString(arguments, "contactId");
        ContentItem contact = null;

        if (contactId is not null)
        {
            contact = await services.GetRequiredService<IContentManager>().GetAsync(contactId, VersionOptions.Latest);

            if (contact is null)
            {
                return $"Nothing was queued: no contact has the ID '{contactId}'.";
            }
        }

        // The processor screens again when it places the activity; refusing now what is already known saves a queued
        // activity that would only fail, and tells the person why straight away.
        var refusal = await ScreenAsync(services, channel, to, contact, cancellationToken);

        if (refusal is not null)
        {
            return "Nothing was queued: " + refusal;
        }

        var activity = await services.GetRequiredService<AIActivityStarter>().StartAsync(new AIActivityStartRequest
        {
            Channel = channel,
            AIProfileId = profileId,
            Destination = to,
            ChannelEndpointId = fromAddressId,
            CampaignId = campaignId,
            SubjectContentType = subject,
            ContactContentItemId = contact?.ContentItemId,
            Source = Source,
            ScheduledUtc = scheduledUtc,
        });

        var now = services.GetRequiredService<IClock>().UtcNow;

        return AIModuleTools.Json(new
        {
            queued = true,
            activityId = activity.ItemId,
            kind = activity.Kind.ToString(),
            channel = activity.Channel,
            status = activity.Status.ToString(),
            to = activity.PreferredDestination,
            contactId = activity.ContactContentItemId,
            scheduled = await AIModuleTools.ToLocalAsync(services.GetService<ILocalClock>(), activity.ScheduledUtc),
            message = activity.ScheduledUtc > now.AddMinutes(1)
                ? "The activity is scheduled. It is screened for opt-out, do-not-call and numbers not in service when it is placed; read its status with getOmnichannelActivity."
                : "The activity is queued and will be placed shortly, after it is screened for opt-out, do-not-call and numbers not in service; read its status with getOmnichannelActivity.",
        });
    }

    private static async Task<string> ScreenAsync(IServiceProvider services, string channel, string to, ContentItem contact, CancellationToken cancellationToken)
    {
        var how = channel == OmnichannelConstants.Channels.Sms ? "text message" : "phone";

        // The consent service is the owner of "may we reach them?": it asks the contact's flags (followed to every
        // record that shares the number), the opt-outs recorded against the address and the site's do-not-call lists.
        var status = await services.GetRequiredService<IConsentService>().GetAsync(new ConsentQuery
        {
            Channel = channel,
            Address = to,
            ContactId = contact?.ContentItemId,
            Contact = contact,
        }, cancellationToken);

        if (status.HasOptedOut)
        {
            return status.Source == ConsentSourceNames.DoNotCallLists
                ? $"{to} is on one of the site's do-not-call lists, so it is not called or texted."
                : $"{to} has asked not to be reached by {how} (opted out). Opted-out numbers are skipped.";
        }

        var notInService = services.GetService<INotInServiceNumberService>();

        if (notInService is not null && await notInService.IsNotInServiceAsync(to, cancellationToken))
        {
            return $"{to} is marked as not in service. Clear the mark with setNumberNotInService if the number works again.";
        }

        return null;
    }
}
