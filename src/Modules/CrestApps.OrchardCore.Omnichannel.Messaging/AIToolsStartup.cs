using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Omnichannel.Messaging.AI;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the messaging workspace AI tools whenever the AI feature is on, and the message templates as the
/// <c>messageTemplate</c> record kind of the generic record tools, read and written through the template editor with the
/// permission the Templates page checks. Every tool checks the caller's workspace permission itself, and each
/// conversation tool checks the conversation operation on the conversation, as the workspace does.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
[Feature(MessagingConstants.Feature.Workspace)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListMessagingConversationsTool>(ListMessagingConversationsTool.TheName)
            .WithTitle(S["List Messaging Conversations"])
            .WithDescription(S["Lists messaging conversations the caller may see, filtered by scope (mine, unassigned, all), channel, unread, holder, queue, campaign and open, with last message preview and first-response state. The previews are customer messages and are sent to the model provider."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddCoreAITool<GetMessagingConversationTool>(GetMessagingConversationTool.TheName)
            .WithTitle(S["Get Messaging Conversation"])
            .WithDescription(S["Returns one conversation with its recent messages, contact, assignment, status, summary and history of transfers. The messages are the customer's own words and are sent to the model provider."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddCoreAITool<SendMessagingMessageTool>(SendMessagingMessageTool.TheName)
            .WithTitle(S["Send Messaging Message"])
            .WithDescription(S["Sends a message to a customer, either as a reply in an existing conversation or as a new conversation from a chosen address to one recipient. Refused when the recipient opted out, the address is invalid, or quiet hours apply."])
            .WithCategory(S["Messaging"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<UpdateMessagingConversationTool>(UpdateMessagingConversationTool.TheName)
            .WithTitle(S["Update Messaging Conversation"])
            .WithDescription(S["Changes a conversation's handling state: claim it, set status open/snoozed/closed, mark it read, or set/clear it as a favorite."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddCoreAITool<TransferMessagingConversationTool>(TransferMessagingConversationTool.TheName)
            .WithTitle(S["Transfer Messaging Conversation"])
            .WithDescription(S["Hands a conversation to another agent or to a queue with an optional note. Use the listed transfer targets; an agent who cannot take it is refused."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddCoreAITool<SetMessagingAvailabilityTool>(SetMessagingAvailabilityTool.TheName)
            .WithTitle(S["Set Messaging Availability"])
            .WithDescription(S["Sets whether the calling user is available to be assigned new messaging conversations."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddAIRecordKind<MessageTemplate>("messageTemplate", kind => kind
            .WithTitle(S["Message template"])
            .WithDescription(S["a reusable reply (name and body) an agent drops into a composer, with the kind of work it is for (Scope, empty for the messaging workspace) and whether its Liquid placeholders, such as the contact's name, are filled in (FillsPlaceholders)"])
            .WithPermission(MessagingPermissions.ManageMessaging)
            .UseCatalogManager<IMessageTemplateManager>()
            .WithProjection(template => new
            {
                id = template.ItemId,
                name = template.Name,
                body = template.Body,
                scope = string.IsNullOrEmpty(template.Scope) ? null : template.Scope,
                fillsPlaceholders = template.FillsPlaceholders,
            })
            .UseEditorReplay());

        services.AddCoreAITool<ListMessagingBroadcastsTool>(ListMessagingBroadcastsTool.TheName)
            .WithTitle(S["List Messaging Broadcasts"])
            .WithDescription(S["Lists broadcasts with status and sent, failed and skipped counts."])
            .WithCategory(S["Messaging"])
            .Selectable();

        services.AddCoreAITool<SendMessagingBroadcastTool>(SendMessagingBroadcastTool.TheName)
            .WithTitle(S["Send Messaging Broadcast"])
            .WithDescription(S["Queues one message to many existing CRM contacts (by contact ID, never typed numbers) from a chosen address; each contact gets an individual 1:1 thread at their own address. Opted-out numbers are skipped and text messages wait for quiet hours to end. Costs carrier fees per message."])
            .WithCategory(S["Messaging"])
            .RequireApproval()
            .Selectable();
    }
}
