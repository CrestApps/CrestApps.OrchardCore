using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Workflows;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Sms;
using OrchardCore.Workflows.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const string _conclusionSystemMessage =
    """
    # GPT System Message – Conversation Disposition Selector (ID Output)

    You are an AI model responsible for analyzing **customer support chat summaries** between a **Customer (User)** and an **AI Assistant (acting on behalf of a contact center agent)**. Your goal is to determine whether the conversation has reached a **conclusion**, and if it has, return the **ID** of the appropriate disposition from a provided list.

    ## Inputs
    1. **Chat Summary** – a summarized transcript of the conversation between the customer and the assistant.  
    2. **Campaign Goal** – the overall purpose or objective of the campaign (e.g., sales, technical support, appointment booking).  
    3. **List of Dispositions** – a list of objects, each with:
       - `Id` (unique identifier)
       - `Name` (friendly name of the disposition)
       - `Description` (optional description of the disposition)

    	Example:
    	[
    	  {"Id": "1", "Name": "Sale Completed", "Description": "Customer completed a purchase"},
    	  {"Id": "2", "Name": "Appointment Scheduled", "Description": "Customer scheduled an appointment"},
    	  {"Id": "3", "Name": "Customer Uninterested", "Description": "Customer declined the offer"},
    	  {"Id": "4", "Name": "Follow-up Required", "Description": "Conversation requires follow-up"}
    	]

    ## Task Instructions

    1. **Determine if the conversation is concluded.**

       * A conversation is **concluded** when the customer has reached a clear end state relative to the campaign goal.
       * If the conversation is **ongoing** (e.g., waiting for customer response, unresolved issue, AI still assisting), it is **not concluded**.

    2. **If the conversation is concluded:**

       * **Pick exactly one disposition** from the provided list that best matches the conversation’s outcome.
       * **Return only the `Id`** of the selected disposition.
       * **Do not create or invent new dispositions** — use only the IDs from the provided list.

    3. **If the conversation is not concluded:**

       * Return an **empty result** (`null`) for the `DispositionId` and mark `Concluded` as `false`.

    ## Output Format

    Return your answer directly as a JSON object, without wrapping it inside any additional object like `data` or other fields:

    {
      "Concluded": true | false,
      "DispositionId": "<id_of_matching_disposition_or_null>"
    }

    ### Examples

    **Concluded conversation**
    {
      "Concluded": true,
      "DispositionId": "2"
    }

    **Ongoing conversation**
    {
      "Concluded": false,
      "DispositionId": null
    }

    ## Evaluation Notes

    * Focus on the **resolution of the conversation** in relation to the **campaign goal**.
    * Ignore irrelevant small talk or details.
    * **Choose only one disposition** from the list; do not guess or invent.
    * If unsure, return `"Concluded": false` rather than making an incorrect assignment.
    """;

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAICompletionService _aICompletionService;
    private readonly IOmnichannelChannelEndpointManager _channelEndpointsManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ISession _session;
    private readonly ISmsService _smsService;
    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAICompletionService aICompletionService,
        IOmnichannelChannelEndpointManager channelEndpointsManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ISession session,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        ILogger<SmsOmnichannelEventHandler> logger,
        IStringLocalizer<SmsOmnichannelEventHandler> stringLocalizer)
    {
        _chatSessionManager = chatSessionManager;
        _aICompletionService = aICompletionService;
        _channelEndpointsManager = channelEndpointsManager;
        _campaignManager = campaignManager;
        _session = session;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task HandleAsync(OmnichannelEvent omnichannelEvent)
    {
        if (omnichannelEvent.EventType != OmnichannelConstants.Events.SmsReceived &&
            omnichannelEvent.Message.Channel == OmnichannelConstants.Channels.Sms &&
            omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        var serviceAddress = omnichannelEvent.Message.ServiceAddress.GetCleanedPhoneNumber();

        var endpoint = await _channelEndpointsManager.GetByServiceAddressAsync(omnichannelEvent.Message.Channel, serviceAddress);

        if (endpoint is null)
        {
            _logger.LogWarning("No channel endpoint found for incoming SMS message. Channel: {Channel}, Service Address: {ServiceAddress}", omnichannelEvent.Message.Channel, omnichannelEvent.Message.ServiceAddress);

            return;
        }

        var activity = await _omnichannelActivityStore.GetAsync(omnichannelEvent.Message.Channel,
            endpoint.ItemId,
            omnichannelEvent.Message.CustomerAddress,
            ActivityInteractionType.Automated);

        if (activity is null)
        {
            _logger.LogWarning("Unable to link incoming SMS message from a customer to an Activity. Channel: {Channel}, Service Address: {ServiceAddress}, Customer Address: {CustomerAddress}", omnichannelEvent.Message.Channel, omnichannelEvent.Message.ServiceAddress, omnichannelEvent.Message.CustomerAddress);

            return;
        }

        // Always set the activity status to AwaitingAgentResponse when a new message is received from the customer to ensure we don't miss responding to them.
        activity.Status = ActivityStatus.AwaitingAgentResponse;

        await _omnichannelActivityStore.UpdateAsync(activity);

        if (string.IsNullOrWhiteSpace(activity.CampaignId))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an CampaignId associated with it. Cannot process incoming SMS message.", activity.ItemId);

            return;
        }

        var campaign = await _campaignManager.FindByIdAsync(activity.CampaignId);

        if (campaign == null)
        {
            _logger.LogWarning("The Campaign {CampaignId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.CampaignId, activity.ItemId);

            return;
        }

        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an AI Session associated with it. Cannot process incoming SMS message.", activity.ItemId);

            return;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId);

        if (chatSession is null)
        {
            _logger.LogWarning("The AI Chat Session {AISessionId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.AISessionId, activity.ItemId);

            return;
        }

        chatSession.Prompts.Add(new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.User,
            Content = omnichannelEvent.Message.Content
        });

        // TODO: add a way to extract data from the message when needed to update Subject or the Contact objects.
        // Maybe in the campaign we can see if updating the subject or contact should be allowed and use AI to extract the data.

        string bestChoice = null;

        try
        {
            var transcript = chatSession.Prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

            var completion = await _aICompletionService.CompleteAsync(campaign.ProviderName, transcript, new AICompletionContext
            {
                ConnectionName = campaign.ConnectionName,
                DeploymentId = campaign.DeploymentName,
                Temperature = campaign.Temperature,
                TopP = campaign.TopP,
                FrequencyPenalty = campaign.FrequencyPenalty,
                PresencePenalty = campaign.PresencePenalty,
                MaxTokens = campaign.MaxTokens,
                ToolNames = campaign.ToolNames,
                Session = chatSession,
            });

            bestChoice = completion?.Messages?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(bestChoice))
            {
                _logger.LogWarning("AI Completion did not return any content for Activity {ActivityId} using AI Campaign {CampaignId}.", activity.ItemId, campaign.ItemId);

                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Completion failed for Activity {ActivityId} using AI Campaign {CampaignId}.", activity.ItemId, campaign.ItemId);
        }

        try
        {
            var result = await _smsService.SendAsync(new SmsMessage
            {
                To = activity.PreferredDestination,
                Body = bestChoice,
            });

            if (result.Succeeded)
            {
                chatSession.Prompts.Add(new AIChatSessionPrompt
                {
                    Id = IdGenerator.GenerateId(),
                    Role = ChatRole.Assistant,
                    Content = bestChoice,
                });

                activity.Status = ActivityStatus.AwaitingCustomerAnswer;

                await _omnichannelActivityStore.UpdateAsync(activity);

                ShellScope.AddDeferredTask(async scope =>
                {
                    // In a deferred task, we check the status of the converation and concluded it if needed.
                    // we use deferred task here to ensure that we don't hold current process for a longer running
                    // AI conclusion detection.
                    var store = scope.ServiceProvider.GetRequiredService<IOmnichannelActivityStore>();
                    var completionService = scope.ServiceProvider.GetRequiredService<IAICompletionService>();
                    var dispositionCatalog = scope.ServiceProvider.GetRequiredService<ICatalog<OmnichannelDisposition>>();
                    var clientFactory = scope.ServiceProvider.GetRequiredService<IAIClientFactory>();
                    var dispositions = await dispositionCatalog.GetAsync(campaign.DispositionIds);
                    var client = await clientFactory.CreateChatClientAsync(campaign.ProviderName, campaign.ConnectionName, campaign.DeploymentName);

                    // TODO, use the AI model to update the subject and the contact if the settings allows for it.
                    var transcript = new List<ChatMessage>
                    {
                        new (ChatRole.System, _conclusionSystemMessage),
                        new (ChatRole.User,
                        $"""
                        Chat Summary: {JsonSerializer.Serialize(chatSession.Prompts)}

                        Campaign Goal: {campaign.CampaignGoal}

                        List of Dispositions: {JsonSerializer.Serialize(dispositions.Select(x => new { Id = x.ItemId, Name = x.DisplayText, x.Description}))}
                        """),
                    };

                    var result = await client.GetResponseAsync<ConverationConclusionResult>(transcript);

                    if (result.Result is not null && result.Result.Concluded)
                    {
                        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                        var workflowManager = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();
                        var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();

                        var omnichannelActivity = await store.FindByIdAsync(activity.ItemId);

                        omnichannelActivity.Status = ActivityStatus.Completed;
                        omnichannelActivity.CompletedUtc = clock.UtcNow;
                        omnichannelActivity.DispositionId = result.Result.DispositionId;
                        omnichannelActivity.CompletedById = omnichannelActivity.AssignedToId;
                        omnichannelActivity.CompletedByUsername = omnichannelActivity.AssignedToUsername;

                        await _omnichannelActivityStore.UpdateAsync(omnichannelActivity);

                        var disposition = await _omnichannelActivityStore.FindByIdAsync(activity.DispositionId);

                        var subject = activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                        var contact = await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                        var input = new Dictionary<string, object>
                        {
                            { "Activity", activity },
                            { "Contact", contact },
                            { "Subject", subject },
                            { "Disposition", disposition },
                        };

                        await workflowManager.TriggerEventAsync(nameof(CompletedActivityEvent), input, correlationId: activity.ItemId);
                    }
                });

                await _session.SaveAsync(chatSession);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS message to {To} for Activity {ActivityId}.", activity.PreferredDestination, activity.ItemId);
        }

        await _session.SaveAsync(chatSession);
    }

    private sealed class ConverationConclusionResult
    {
        public bool Concluded { get; set; }
        public string DispositionId { get; set; }
    }
}
