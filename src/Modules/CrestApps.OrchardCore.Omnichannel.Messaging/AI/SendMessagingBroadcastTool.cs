using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Queues a broadcast with the checks of the Broadcasts page. Creating the broadcast is what queues it; the Outreach
/// engine sends it in the background through the conversation service, which skips recipients who opted out, and it
/// holds text messages until quiet hours end. Unlike the page, the tool takes existing CRM contacts only, never numbers
/// or addresses typed into the chat: each contact is messaged at their own address on the channel.
/// </summary>
internal sealed class SendMessagingBroadcastTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendMessagingBroadcast";

    /// <summary>
    /// The most recipients one call queues.
    /// </summary>
    public const int MaxRecipients = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "A name for the broadcast, shown on the Broadcasts page."
        },
        "fromAddressId": {
          "type": "string",
          "description": "The ID of our address to send from. It must be on a channel that supports broadcasts."
        },
        "contactIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 500,
          "description": "The content item IDs of the existing CRM contacts to message. Each is messaged at their own address on the sending address's channel (for text messages, their mobile number first). Numbers or addresses typed into the chat are not accepted."
        },
        "body": {
          "type": "string",
          "description": "The message text."
        }
      },
      "required": ["name", "fromAddressId", "contactIds", "body"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues one message to many existing CRM contacts (by contact ID, never typed numbers) from a chosen address; each contact gets an individual 1:1 thread at their own address. Opted-out numbers are skipped and text messages wait for quiet hours to end. Costs carrier fees per message.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => MessagingPermissions.SendGroupMessages;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "name");
        var body = ReadString(arguments, "body");
        var contactIds = ReadStrings(arguments, "contactIds");

        if (name is null)
        {
            return "Nothing was queued: a name is required.";
        }

        if (body is null)
        {
            return "Nothing was queued: a message body is required.";
        }

        if (contactIds.Count == 0)
        {
            return "Nothing was queued: give at least one contact in contactIds.";
        }

        if (contactIds.Count > MaxRecipients)
        {
            return $"Nothing was queued: give at most {MaxRecipients} contacts in one call, or use the Broadcasts page or an activity load for more.";
        }

        var endpointManager = services.GetRequiredService<IOmnichannelChannelEndpointManager>();
        var channelResolver = services.GetRequiredService<IMessagingChannelResolver>();
        var fromAddressId = ReadString(arguments, "fromAddressId");
        var endpoint = fromAddressId is null ? null : await endpointManager.FindByIdAsync(fromAddressId, cancellationToken);

        // The channel the composer and the Broadcasts page send on: the first messaging channel the address has.
        var sendingChannel = endpoint is null ? null : channelResolver.GetAll().FirstOrDefault(candidate => endpoint.HasCapability(candidate.Name));

        if (sendingChannel is null)
        {
            return FromAddressRefusal;
        }

        // Only existing contacts: each is messaged at their own address on the channel, so nothing typed into the chat
        // becomes a recipient.
        var contactTypes = await services.GetRequiredService<IOmnichannelContactTypeProvider>().GetContactContentTypesAsync(cancellationToken);
        var contacts = (await services.GetRequiredService<IContentManager>().GetAsync(contactIds, VersionOptions.Latest))
            .Where(item => item is not null && contactTypes.Contains(item.ContentType, StringComparer.OrdinalIgnoreCase))
            .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var unknown = contactIds.Where(id => !contacts.ContainsKey(id)).ToList();

        if (unknown.Count > 0)
        {
            return $"Nothing was queued: these are not IDs of CRM contacts: {string.Join(", ", unknown)}. Find the contacts first, for example with listContentItems; numbers typed into the chat are not accepted.";
        }

        var addresses = new List<string>();
        var withoutAddress = new List<string>();

        foreach (var contactId in contactIds)
        {
            var contactAddresses = sendingChannel.GetContactAddresses(contacts[contactId]);

            if (contactAddresses.Count == 0)
            {
                withoutAddress.Add(contactId);
            }
            else
            {
                addresses.Add(contactAddresses[0]);
            }
        }

        if (withoutAddress.Count > 0)
        {
            return $"Nothing was queued: these contacts have no {sendingChannel.DisplayName.Value} address: {string.Join(", ", withoutAddress)}. Leave them out or add the address on the contact first.";
        }

        var workspaceBuilder = services.GetRequiredService<MessagingWorkspaceBuilder>();
        var check = await MessagingRecipientRules.CheckAsync(
            endpointManager,
            channelResolver,
            services.GetRequiredService<MessagingContactSearch>(),
            workspaceBuilder,
            user,
            fromAddressId,
            recipientsText: null,
            addresses,
            cancellationToken);

        if (check.Endpoint is null || check.Channel is null)
        {
            return FromAddressRefusal;
        }

        var channel = check.Channel;
        var problems = new List<string>();

        if (!channel.Capabilities.SupportsBroadcast)
        {
            problems.Add($"{channel.DisplayName.Value} does not support broadcasts.");
        }

        if (check.InvalidAddresses.Count > 0)
        {
            problems.Add($"These are not valid {channel.DisplayName.Value} addresses: {string.Join(", ", check.InvalidAddresses)}.");
        }
        else if (check.UnlistedAddresses.Count > 0)
        {
            problems.Add($"These contact addresses are not valid {channel.DisplayName.Value} addresses: {string.Join(", ", check.UnlistedAddresses)}.");
        }

        // Each recipient is written to in their own conversation, and one that already exists may belong to a colleague
        // or another queue. The broadcast is sent later with nobody signed in to check, so it is checked now.
        if (check.UnreachableRecipients.Count > 0)
        {
            problems.Add($"These recipients already have a conversation you cannot send on, handled by a colleague or another queue: {string.Join(", ", check.UnreachableRecipients)}.");
        }

        if (problems.Count > 0)
        {
            return "Nothing was queued: " + string.Join(" ", problems);
        }

        var agent = await workspaceBuilder.GetCurrentAgentAsync(user);
        var manager = services.GetRequiredService<IMessagingBroadcastManager>();
        var broadcast = await manager.NewAsync(cancellationToken: cancellationToken);
        broadcast.ItemId = UniqueId.GenerateId();
        broadcast.Name = name;
        broadcast.Channel = channel.Name;
        broadcast.ServiceAddress = check.Endpoint.Value;
        broadcast.Body = body;
        broadcast.Recipients = check.Recipients.ToList();
        broadcast.OwnerAgentId = agent?.ItemId;
        broadcast.Status = MessagingBroadcastStatus.Queued;

        await manager.CreateAsync(broadcast, cancellationToken);

        return Json(new
        {
            queued = true,
            message = $"The broadcast was queued and will be sent to {broadcast.Recipients.Count} contact(s) in the background. Opted-out numbers are skipped; read the counts with listMessagingBroadcasts.",
            broadcast = await ListMessagingBroadcastsTool.DescribeAsync(services.GetService<ILocalClock>(), broadcast),
        });
    }

    private const string FromAddressRefusal = "Nothing was queued: fromAddressId must be the ID of an address that can send messages. Use listRecords with the kind omnichannelAddress, or sendMessagingMessage without fromAddressId, to list them.";

    private static List<string> ReadStrings(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        IEnumerable<string> values = value switch
        {
            string text => [text],
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()),
            JsonElement element => [element.ToString()],
            IEnumerable<string> texts => texts,
            IEnumerable<object> items => items.Select(item => item?.ToString()),
            _ => [value.ToString()],
        };

        return values
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
