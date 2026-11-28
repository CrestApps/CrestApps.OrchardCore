using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Sends a message to a customer through the conversation service the workspace sends with: a reply in an existing
/// conversation, or a new conversation to one recipient checked as the composer checks it. The service refuses a
/// contact who opted out of the channel on every send. Unlike the workspace, which only warns, the tool refuses to send
/// outside the queue's business hours unless the caller may send during quiet hours.
/// </summary>
internal sealed class SendMessagingMessageTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendMessagingMessage";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "conversationId": {
          "type": "string",
          "description": "The ID of the conversation to reply in. Leave it out to message a recipient with to."
        },
        "to": {
          "type": "string",
          "description": "Instead of conversationId: the one recipient of a new message, such as +15550107788. A recipient who already has a conversation on the channel is written to in it."
        },
        "fromAddressId": {
          "type": "string",
          "description": "With to: the ID of our address to send from. Leave it out when only one address can send messages; otherwise the tool lists them."
        },
        "body": {
          "type": "string",
          "description": "The message text."
        }
      },
      "required": ["body"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends a message to a customer, either as a reply in an existing conversation or as a new conversation from a chosen address to one recipient. Refused when the recipient opted out, the address is invalid, or quiet hours apply and the user may not send during them. Reaches a real customer and costs carrier fees.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var body = ReadString(arguments, "body");

        if (body is null)
        {
            return "Nothing was sent: a message is required.";
        }

        var conversationId = ReadString(arguments, "conversationId");
        var to = ReadString(arguments, "to");

        if ((conversationId is null) == (to is null))
        {
            return "Nothing was sent: give either the conversationId to reply in or the one recipient in to.";
        }

        return conversationId is not null
            ? await ReplyAsync(services, user, conversationId, body, cancellationToken)
            : await StartAsync(arguments, services, user, to, body, cancellationToken);
    }

    private static async Task<string> ReplyAsync(IServiceProvider services, ClaimsPrincipal user, string conversationId, string body, CancellationToken cancellationToken)
    {
        var workspaceBuilder = services.GetRequiredService<MessagingWorkspaceBuilder>();
        var conversation = await services.GetRequiredService<IMessagingConversationStore>().FindByIdAsync(conversationId, cancellationToken);

        if (conversation is null || !await workspaceBuilder.AuthorizeAsync(user, conversation, ConversationOperation.View))
        {
            return "Nothing was sent: no conversation you may open has that ID. Call listMessagingConversations to find it.";
        }

        if (!await workspaceBuilder.AuthorizeAsync(user, conversation, ConversationOperation.Send))
        {
            return "Nothing was sent: you are not allowed to send on this conversation; a colleague or another queue handles it.";
        }

        var quietHours = await CheckQuietHoursAsync(services, user, conversation, cancellationToken);

        if (quietHours is not null)
        {
            return quietHours;
        }

        var agent = await workspaceBuilder.GetCurrentAgentAsync(user);
        var result = await services.GetRequiredService<IMessagingConversationService>().SendAsync(new MessagingSendRequest
        {
            ConversationId = conversation.ItemId,
            Body = body,
            ActingAgentId = agent?.ItemId,
            Principal = user,
        }, cancellationToken);

        return Describe(result, conversation.ItemId);
    }

    private static async Task<string> StartAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, string to, string body, CancellationToken cancellationToken)
    {
        var workspaceBuilder = services.GetRequiredService<MessagingWorkspaceBuilder>();
        var endpointId = ReadString(arguments, "fromAddressId");

        if (endpointId is null)
        {
            // The composer's own list of the addresses a message can leave from.
            var (items, _) = await workspaceBuilder.BuildEndpointOptionsAsync(null);

            if (items.Count != 1)
            {
                return items.Count == 0
                    ? "Nothing was sent: no address can send messages. An administrator adds one under Omnichannel Addresses."
                    : Json(new
                    {
                        message = "Nothing was sent: choose the address to send from and call again with its ID in fromAddressId.",
                        addresses = items.Select(item => new { id = item.Value, name = item.Text, channel = item.Group?.Name }),
                    });
            }

            endpointId = items[0].Value;
        }

        var check = await MessagingRecipientRules.CheckAsync(
            services.GetRequiredService<IOmnichannelChannelEndpointManager>(),
            services.GetRequiredService<IMessagingChannelResolver>(),
            services.GetRequiredService<MessagingContactSearch>(),
            workspaceBuilder,
            user,
            endpointId,
            recipientsText: null,
            [to],
            cancellationToken);

        if (check.Endpoint is null || check.Channel is null)
        {
            return $"Nothing was sent: no address with the ID '{endpointId}' can send messages. Leave fromAddressId out to list the addresses.";
        }

        var channel = check.Channel;

        if (check.InvalidAddresses.Count > 0)
        {
            return $"Nothing was sent: {string.Join(", ", check.InvalidAddresses)} is not a valid {channel.DisplayName.Value} address.";
        }

        if (check.UnlistedAddresses.Count > 0)
        {
            return channel.Capabilities.AddressesArePhoneNumbers
                ? $"Nothing was sent: {string.Join(", ", check.UnlistedAddresses)} is not in your contacts, so it must be in international format, for example +12015550123."
                : $"Nothing was sent: {string.Join(", ", check.UnlistedAddresses)} is not in your contacts and is not a valid {channel.DisplayName.Value} address.";
        }

        if (check.UnreachableRecipients.Count > 0)
        {
            return "Nothing was sent: this recipient already has a conversation you cannot send on, handled by a colleague or another queue.";
        }

        var recipient = check.Recipients[0];

        // A recipient with a conversation is written to in it, so it is held to that conversation's business hours.
        var existing = await services.GetRequiredService<IMessagingConversationStore>().FindByContactAsync(channel.Name, recipient, cancellationToken);

        if (existing is not null)
        {
            var quietHours = await CheckQuietHoursAsync(services, user, existing, cancellationToken);

            if (quietHours is not null)
            {
                return quietHours;
            }
        }

        var agent = await workspaceBuilder.GetCurrentAgentAsync(user);
        var result = await services.GetRequiredService<IMessagingConversationService>()
            .SendDirectAsync(channel.Name, check.Endpoint.Value, recipient, body, agent?.ItemId, cancellationToken);

        return Describe(result, result.Message?.ConversationId);
    }

    private static async Task<string> CheckQuietHoursAsync(IServiceProvider services, ClaimsPrincipal user, MessagingConversation conversation, CancellationToken cancellationToken)
    {
        var channel = services.GetRequiredService<IMessagingChannelResolver>().Get(conversation.Channel);

        if (channel?.Capabilities.ObservesQuietHours != true)
        {
            return null;
        }

        var decision = await services.GetRequiredService<MessagingQuietHoursGuard>().EvaluateAsync(conversation, cancellationToken);

        if (!decision.IsQuietHours ||
            await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, MessagingPermissions.SendDuringQuietHours))
        {
            return null;
        }

        return $"Nothing was sent: {decision.Reason} Sending outside business hours needs the Send messages outside business hours permission; try again when the queue is open.";
    }

    private static string Describe(MessagingSendResult result, string conversationId)
    {
        if (result.Message is null)
        {
            return $"Nothing was sent: {result.Error}";
        }

        return Json(new
        {
            sent = result.Succeeded,
            conversationId,
            messageId = result.Message.Id,
            deliveryStatus = result.Message.DeliveryStatus,
            error = result.Succeeded ? null : $"The message was recorded but the provider did not accept it: {result.Error}",
        });
    }
}
