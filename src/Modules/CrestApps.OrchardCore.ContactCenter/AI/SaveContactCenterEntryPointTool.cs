using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates or updates an inbound entry point, including its phone menu, through the entry point manager, whose handler
/// validates it (target, addresses and the menu) as it validates the entry point editor's post, after the editors' own
/// rules are applied. Settings other features keep on the entry point are never touched, and routing to an AI agent is
/// left to the AI agent's own card on the entry point's page.
/// </summary>
internal sealed class SaveContactCenterEntryPointTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveContactCenterEntryPoint";

    private static readonly string[] _callSettings =
    [
        "priority", "closedAction", "overflowQueueId", "welcomeMessage", "closedMessage", "voicemailEnabled",
        "ringTimeoutSeconds", "voicemailGreetingText", "voicemailDestination", "voicemailRecipientAgentId", "phoneMenu",
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": { "type": "string", "description": "The ID of the entry point to update. Leave it out to create one." },
        "name": { "type": "string", "description": "The entry point's name. Required when creating." },
        "description": { "type": "string", "description": "A description." },
        "channel": { "type": "string", "description": "The channel, such as Phone or SMS. Only when creating; needed when more than one channel can have entry points." },
        "enabled": { "type": "boolean", "description": "Whether the entry point answers its numbers." },
        "addressIds": { "type": "array", "items": { "type": "string" }, "description": "The IDs of the business's own numbers or addresses it answers (see listRecords with the kind omnichannelAddress). Replaces the whole list. A number can be answered by one enabled entry point per channel." },
        "targetType": { "type": "string", "enum": ["Queue", "Agent"], "description": "Whether it routes to a queue or rings one agent. Routing to an AI agent is set on the entry point's page." },
        "targetQueueId": { "type": "string", "description": "The queue it routes to, when targetType is Queue." },
        "targetAgentId": { "type": "string", "description": "The agent profile it rings, when targetType is Agent (see listContactCenterAgents)." },
        "businessHoursCalendarId": { "type": "string", "description": "The business-hours calendar; an empty text makes it always open." },
        "priority": { "type": "string", "enum": ["Lowest", "Low", "Normal", "High", "Highest"], "description": "Call settings: the queue priority of its calls." },
        "closedAction": { "type": "string", "enum": ["HoldInQueue", "Voicemail", "Overflow", "Reject"], "description": "Call settings: what happens to a call while closed." },
        "overflowQueueId": { "type": "string", "description": "Call settings: the queue closed-hours calls overflow to." },
        "welcomeMessage": { "type": "string", "description": "Call settings: the message a caller hears first." },
        "closedMessage": { "type": "string", "description": "Call settings: the message a caller hears while closed." },
        "voicemailEnabled": { "type": "boolean", "description": "Call settings: whether an unanswered call to an agent goes to voicemail." },
        "ringTimeoutSeconds": { "type": "integer", "description": "Call settings: seconds an agent's phone rings before voicemail." },
        "voicemailGreetingText": { "type": "string", "description": "Call settings: the voicemail greeting." },
        "voicemailDestination": { "type": "string", "enum": ["AgentInbox", "QueueSharedBox"], "description": "Call settings: where a queue line's messages go." },
        "voicemailRecipientAgentId": { "type": "string", "description": "Call settings: the agent whose inbox a queue line's messages go to." },
        "phoneMenu": {
          "type": ["object", "null"],
          "description": "Call settings: the whole phone menu, which replaces the current one; null removes it. Read the current menu first with getRecord (kind contactCenterEntryPoint).",
          "properties": {
            "rootNodeId": { "type": "string", "description": "The node callers hear first." },
            "maxRetries": { "type": "integer", "minimum": 1, "description": "How many times a menu is repeated after an invalid key." },
            "fallbackAction": { "type": "object", "description": "What happens when retries run out: kind and targetId, as for an option." },
            "nodes": {
              "type": "array",
              "description": "The menus.",
              "items": {
                "type": "object",
                "properties": {
                  "nodeId": { "type": "string", "description": "A unique ID for the menu." },
                  "prompt": { "type": "string", "description": "What the caller hears." },
                  "promptMediaId": { "type": "string", "description": "A voice media clip played instead of the prompt text." },
                  "options": {
                    "type": "array",
                    "description": "The keys the caller can press.",
                    "items": {
                      "type": "object",
                      "properties": {
                        "digit": { "type": "string", "description": "One telephone key: 0-9, * or #." },
                        "action": {
                          "type": "object",
                          "description": "What the key does.",
                          "properties": {
                            "kind": { "type": "string", "enum": ["RouteToQueue", "RouteToAgent", "Voicemail", "ExternalTransfer", "Repeat", "SubMenu"], "description": "The action." },
                            "targetId": { "type": "string", "description": "The queue ID, agent profile ID, approved external destination ID or sub-menu node ID." }
                          },
                          "required": ["kind"]
                        }
                      },
                      "required": ["digit", "action"]
                    }
                  }
                },
                "required": ["nodeId"]
              }
            }
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a Contact Center inbound entry point, including its phone menu. Only the fields given are changed; the phone menu and address list are replaced whole. The menu is validated (root node, duplicate node IDs, missing prompt or options, invalid or duplicate keys, missing action target, unknown sub-menu). A saved change takes effect on the next inbound call or message to those numbers.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageQueues;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IContactCenterEntryPointManager>();
        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");
        var entryPoint = id is null ? null : await manager.FindByIdAsync(id, cancellationToken);

        if (id is not null && entryPoint is null)
        {
            return $"Nothing was saved: no entry point has the ID '{id}'. Use listRecords with the kind contactCenterEntryPoint to find it.";
        }

        if (reader.GetString("targetType") is { } requestedTarget && string.Equals(requestedTarget, nameof(EntryPointTargetType.AIAgent), StringComparison.OrdinalIgnoreCase))
        {
            var path = entryPoint is null ? null : GetAdminPath(services, "EntryPoints", "Edit", entryPoint.ItemId);

            return "Nothing was saved: routing to an AI agent is set on the AI agent's card of the entry point's page" + (path is null ? "." : $": {path}");
        }

        var channel = ResolveChannel(services, reader, entryPoint);

        if (channel.Error is not null)
        {
            return "Nothing was saved: " + channel.Error;
        }

        // The call settings and the phone menu are edited on the call entry point's own card, which the Inbound Voice
        // feature adds for the phone channel.
        var callSettingsApply = string.Equals(channel.Name ?? "Phone", OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) &&
            services.GetService<ShellDescriptor>()?.Features.Any(feature => feature.Id == ContactCenterConstants.Feature.InboundVoice) == true;

        if (!callSettingsApply && _callSettings.FirstOrDefault(reader.Has) is { } callSetting)
        {
            return $"Nothing was saved: {callSetting} is a call setting, which only a Phone entry point has while the Contact Center Inbound Voice feature is enabled.";
        }

        if (entryPoint is null)
        {
            reader.Changes[nameof(ContactCenterEntryPoint.Channel)] = channel.Name;
        }

        reader.Text("name", nameof(ContactCenterEntryPoint.Name), required: true);
        reader.Text("description", nameof(ContactCenterEntryPoint.Description));
        reader.Bool("enabled", nameof(ContactCenterEntryPoint.Enabled));
        reader.IdList("addressIds", nameof(ContactCenterEntryPoint.AddressIds));
        reader.Choice<EntryPointTargetType>("targetType", nameof(ContactCenterEntryPoint.TargetType), [EntryPointTargetType.Queue, EntryPointTargetType.Agent]);
        reader.Text("targetQueueId", nameof(ContactCenterEntryPoint.TargetQueueId));
        reader.Text("targetAgentId", nameof(ContactCenterEntryPoint.TargetAgentId));
        reader.Text("businessHoursCalendarId", nameof(ContactCenterEntryPoint.BusinessHoursCalendarId));
        reader.Choice<InteractionPriority>("priority", nameof(ContactCenterEntryPoint.Priority));
        reader.Choice<EntryPointClosedAction>("closedAction", nameof(ContactCenterEntryPoint.ClosedAction));
        reader.Text("overflowQueueId", nameof(ContactCenterEntryPoint.OverflowQueueId));
        reader.Text("welcomeMessage", nameof(ContactCenterEntryPoint.WelcomeMessage));
        reader.Text("closedMessage", nameof(ContactCenterEntryPoint.ClosedMessage));
        reader.Bool("voicemailEnabled", nameof(ContactCenterEntryPoint.VoicemailEnabled));
        reader.Int("ringTimeoutSeconds", nameof(ContactCenterEntryPoint.RingTimeoutSeconds), ContactCenterConstants.DirectRouting.MinimumRingTimeoutSeconds, ContactCenterConstants.DirectRouting.MaximumRingTimeoutSeconds);
        reader.Text("voicemailGreetingText", nameof(ContactCenterEntryPoint.VoicemailGreetingText));
        reader.Choice<EntryPointVoicemailDestination>("voicemailDestination", nameof(ContactCenterEntryPoint.VoicemailDestination));
        reader.Text("voicemailRecipientAgentId", nameof(ContactCenterEntryPoint.VoicemailRecipientAgentId));

        if (reader.Has("phoneMenu"))
        {
            var menu = reader.GetNode("phoneMenu");

            if (menu is not null and not JsonObject)
            {
                reader.Errors.Add("phoneMenu must be an object, or null to remove the menu.");
            }

            reader.Changes[nameof(ContactCenterEntryPoint.IvrFlow)] = menu;
        }

        if (reader.Errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", reader.Errors);
        }

        var missing = await FindMissingReferencesAsync(services, reader.Changes, cancellationToken);

        if (missing.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", missing);
        }

        var result = await ContactCenterCatalogWriter.SaveAsync(
            manager,
            entryPoint,
            reader.Changes,
            record =>
            {
                ContactCenterEntryPointEditorRules.ApplyTarget(record);

                if (callSettingsApply)
                {
                    ContactCenterEntryPointEditorRules.ApplyVoice(record, record.TargetType);
                }
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            return result.Message;
        }

        var describer = await ContactCenterEntryPointDescriber.CreateAsync(services, cancellationToken);

        return Json(new
        {
            saved = entryPoint is null ? "created" : "updated",
            changed = reader.Changes.Select(change => change.Key).ToList(),
            entryPoint = await describer.DescribeAsync(result.Record),
        });
    }

    private static (string Name, string Error) ResolveChannel(IServiceProvider services, ContactCenterToolArguments reader, ContactCenterEntryPoint entryPoint)
    {
        var requested = reader.GetString("channel");

        if (entryPoint is not null)
        {
            return requested is not null && !string.Equals(requested, entryPoint.GetChannel(), StringComparison.OrdinalIgnoreCase)
                ? (null, "an entry point's channel cannot change. Create another entry point for the other channel.")
                : (entryPoint.GetChannel(), null);
        }

        // As on the Create page: with one channel it is the one; with none, the entry point answers calls.
        var channels = services.GetRequiredService<IOptions<EntryPointChannelOptions>>().Value.Channels;

        if (requested is null)
        {
            return channels.Count switch
            {
                0 => (null, null),
                1 => (channels.Values.First().Name, null),
                _ => (null, $"give the channel: one of {string.Join(", ", channels.Values.Select(channel => channel.Name))}."),
            };
        }

        return channels.TryGetValue(requested, out var channel)
            ? (channel.Name, null)
            : (null, $"'{requested}' is not a channel entry points can answer. Use one of {string.Join(", ", channels.Values.Select(candidate => candidate.Name))}.");
    }

    // The editors offer only existing records in their lists, and a phone menu runs only approved external destinations,
    // so a typed ID that names nothing is refused here instead of being found by a caller.
    private static async Task<List<string>> FindMissingReferencesAsync(IServiceProvider services, JsonObject changes, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        var queueNames = await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken);
        var agentNames = await ContactCenterSetupLookups.GetAgentNamesAsync(services, cancellationToken);

        foreach (var property in new[] { nameof(ContactCenterEntryPoint.TargetQueueId), nameof(ContactCenterEntryPoint.OverflowQueueId) })
        {
            if (changes[property]?.GetValue<string>() is { } queueId && !queueNames.ContainsKey(queueId))
            {
                missing.Add($"No queue has the ID '{queueId}'. Use listRecords with the kind contactCenterQueue to find it.");
            }
        }

        foreach (var property in new[] { nameof(ContactCenterEntryPoint.TargetAgentId), nameof(ContactCenterEntryPoint.VoicemailRecipientAgentId) })
        {
            if (changes[property]?.GetValue<string>() is { } agentId && !agentNames.ContainsKey(agentId))
            {
                missing.Add($"No agent profile has the ID '{agentId}'. Call listContactCenterAgents to find it.");
            }
        }

        if (changes[nameof(ContactCenterEntryPoint.BusinessHoursCalendarId)]?.GetValue<string>() is { } calendarId &&
            !(await ContactCenterSetupLookups.GetCalendarNamesAsync(services, cancellationToken)).ContainsKey(calendarId))
        {
            missing.Add($"No business-hours calendar has the ID '{calendarId}'. Use listRecords with the kind businessHoursCalendar to find it.");
        }

        if (changes[nameof(ContactCenterEntryPoint.IvrFlow)] is JsonObject menu)
        {
            IvrFlow flow;

            try
            {
                flow = menu.Deserialize<IvrFlow>(JOptions.Default);
            }
            catch (JsonException exception)
            {
                missing.Add($"The phone menu has the wrong shape: {exception.Message}");

                return missing;
            }

            var destinations = await GetMenuDestinationsAsync(services);
            var actions = (flow?.Nodes ?? [])
                .Where(node => node is not null)
                .SelectMany(node => (node.Options ?? []).Where(option => option is not null).Select(option => (Where: $"menu '{node.NodeId}' key '{option.Digit}'", option.Action)))
                .Append((Where: "the fallback action", Action: flow?.FallbackAction));

            foreach (var (where, action) in actions)
            {
                if (action is null || string.IsNullOrWhiteSpace(action.TargetId))
                {
                    continue;
                }

                var problem = action.Kind switch
                {
                    IvrActionKind.RouteToQueue when !queueNames.ContainsKey(action.TargetId) => $"In {where}, no queue has the ID '{action.TargetId}'.",
                    IvrActionKind.RouteToAgent when !agentNames.ContainsKey(action.TargetId) => $"In {where}, no agent profile has the ID '{action.TargetId}'.",
                    IvrActionKind.ExternalTransfer when !destinations.Contains(action.TargetId.Trim()) => $"In {where}, '{action.TargetId}' is not an enabled external destination; a phone menu can only transfer to the approved destinations of the Contact Center external transfer settings.",
                    _ => null,
                };

                if (problem is not null)
                {
                    missing.Add(problem);
                }
            }
        }

        return missing;
    }

    // The menu's external transfer reads only the curated catalog: an enabled destination with a number.
    private static async Task<HashSet<string>> GetMenuDestinationsAsync(IServiceProvider services)
    {
        if (services.GetService<ISiteService>() is not { } siteService)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<ContactCenterExternalTransferSettings>();

        return (settings.Destinations ?? [])
            .Where(destination => destination is not null && destination.Enabled && !string.IsNullOrWhiteSpace(destination.Id) && !string.IsNullOrWhiteSpace(destination.E164Address))
            .Select(destination => destination.Id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
