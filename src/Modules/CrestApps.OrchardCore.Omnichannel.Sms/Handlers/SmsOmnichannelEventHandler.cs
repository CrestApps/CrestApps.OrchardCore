using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
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
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Sms;
using OrchardCore.Workflows.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const string _conclusionSystemMessage =
    """
    You are an AI model responsible for analyzing **customer support chat summaries** between a **Customer (User)** and an **AI Assistant (acting on behalf of a contact center agent)**.  

    The **user prompt** you will receive will include:

    Chat Summary: <summary of conversation>
    Campaign Goal: <campaign objective>
    List of Dispositions: <list of dispositions in JSON format>
    Subject: <optional, only present if subject evaluation and update was made>
    Contact: <optional, only present if contact evaluation and update was made>

    ````

    Your primary goal is to determine whether the conversation has reached a **conclusion**, and if it has, return the **ID** of the appropriate disposition from the provided list.

    ---

    ## Task Instructions

    1. **Determine if the conversation is concluded.**
       * A conversation is **concluded** when the customer has reached a clear end state relative to the campaign goal.
       * If the conversation is **ongoing** (e.g., waiting for a response, unresolved issue, or AI still assisting), it is **not concluded**.

    2. **If the conversation is concluded:**
       * Select **exactly one** disposition from the provided list that best matches the conversation's outcome.
       * **Return only the `Id`** of the selected disposition.
       * **Do not create or invent new dispositions** — use only the provided ones.

    3. **If the conversation is not concluded:**
       * Return an **empty result** (`null`) for the `DispositionId` and mark `Concluded` as `false`.

    ---

    ## Subject Update (Optional)

    * Only evaluate the **Subject** if a `Subject:` field is present in the user prompt.
    * Use the most recent conversation context to determine if the subject should be updated.
    * Return `"Subject": null` if no update is needed.
    * Do **not** modify the JSON structure; only replace the value.

    ````

    ---

    ## Contact Update (Optional)

    * Only evaluate the **Contact** if a `Contact:` field is present in the user prompt.
    * Update fields only when the user provides **new or corrected contact information** (ex., name, phone, or email).
    * Return `"Contact": null` if no update is needed.
    * Do **not** modify the JSON structure; only replace the values inside it.

    Example input for contact evaluation:

    ---

    ## Output Format

    Return your answer directly as a JSON object with **all possible fields**, even if some are `null`:

    ```json
    {
      "Concluded": true | false,
      "DispositionId": "<id_of_matching_disposition_or_null>",
      "Subject": "<updated_subject_or_null>",
      "Contact": "<updated_contact_or_null>"
    }
    ```

    * If `Subject` or `Contact` sections are not provided in the user prompt or no updates are required, return them as `null`.
    * Never invent new fields or change the structure of the JSON.
    * Always preserve the provided JSON structure exactly.

    ---

    ## Evaluation Notes

    * Focus on whether the conversation reached a **clear end state** relative to the **campaign goal**.
    * Ignore irrelevant conversation details.
    * If unsure, prefer `"Concluded": false`.
    * Never invent data, and never modify the output schema.

    ```

    This version ensures:

    - The model expects structured **user prompts** instead of embedded JSON.
    - Subject and contact updates are only evaluated **if present**.
    - Clear instructions for handling concluded vs ongoing conversations.
    - Output schema is strictly enforced.  

    If you want, I can also **rewrite this into a fully concise system prompt under 500 words** optimized for GPT usage while keeping all rules intact. Do you want me to do that?
    ```
    
    """;

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _aICompletionService;
    private readonly IOmnichannelChannelEndpointManager _channelEndpointsManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ISession _session;
    private readonly ISmsService _smsService;
    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService aICompletionService,
        IOmnichannelChannelEndpointManager channelEndpointsManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ISession session,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions,
        ILogger<SmsOmnichannelEventHandler> logger,
        IStringLocalizer<SmsOmnichannelEventHandler> stringLocalizer)
    {
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _aICompletionService = aICompletionService;
        _channelEndpointsManager = channelEndpointsManager;
        _campaignManager = campaignManager;
        _session = session;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;
        _jsonSerializerOptions = jsonSerializerOptions.Value;
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

        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = chatSession.SessionId,
            Role = ChatRole.User,
            Content = omnichannelEvent.Message.Content
        });

        // TODO: add a way to extract data from the message when needed to update Subject or the Contact objects.
        // Maybe in the campaign we can see if updating the subject or contact should be allowed and use AI to extract the data.

        string bestChoice = null;

        try
        {
            var prompts = await _promptStore.GetPromptsAsync(chatSession.SessionId);

            var transcript = prompts.Where(x => !x.IsGeneratedPrompt)
                .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

            var context = new AICompletionContext
            {
                ConnectionName = campaign.ConnectionName,
                DeploymentId = campaign.DeploymentName,
                Temperature = campaign.Temperature,
                TopP = campaign.TopP,
                FrequencyPenalty = campaign.FrequencyPenalty,
                PresencePenalty = campaign.PresencePenalty,
                MaxTokens = campaign.MaxTokens,
                ToolNames = campaign.ToolNames,
            };

            context.AdditionalProperties["Session"] = chatSession;

            var completion = await _aICompletionService.CompleteAsync(campaign.ProviderName, transcript, context);

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
                await _promptStore.CreateAsync(new AIChatSessionPrompt
                {
                    ItemId = IdGenerator.GenerateId(),
                    SessionId = chatSession.SessionId,
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
                    var dispositionCatalog = scope.ServiceProvider.GetRequiredService<ICatalog<OmnichannelDisposition>>();
                    var clientFactory = scope.ServiceProvider.GetRequiredService<IAIClientFactory>();
                    var deferredPromptStore = scope.ServiceProvider.GetRequiredService<IAIChatSessionPromptStore>();
                    var dispositions = await dispositionCatalog.GetAsync(campaign.DispositionIds);
                    var client = await clientFactory.CreateChatClientAsync(campaign.ProviderName, campaign.ConnectionName, campaign.DeploymentName);

                    var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();

                    ContentItem subject = null;
                    ContentItem contact = null;

                    var sessionPrompts = await deferredPromptStore.GetPromptsAsync(chatSession.SessionId);

                    var userPrompt = $"""
                        Chat Summary: {JsonSerializer.Serialize(sessionPrompts)}
                        Campaign Goal: {campaign.CampaignGoal}
                        List of Dispositions: {JsonSerializer.Serialize(dispositions.Select(x => new { Id = x.ItemId, Name = x.DisplayText, x.Description }))}
                        """;

                    if (campaign.AllowAIToUpdateSubject)
                    {
                        subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);

                        userPrompt +=
                        $"""
                        Subject: {JsonSerializer.Serialize(activity.Subject, _jsonSerializerOptions.SerializerOptions)}
                        """;
                    }

                    if (campaign.AllowAIToUpdateContact)
                    {
                        contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                        userPrompt +=
                        $"""
                        Contact: {JsonSerializer.Serialize(contact, _jsonSerializerOptions.SerializerOptions)}
                        """;
                    }

                    var transcript = new List<ChatMessage>
                    {
                        new (ChatRole.System, _conclusionSystemMessage),
                        new (ChatRole.User, userPrompt),
                    };

                    var result = await client.GetResponseAsync<ConverationConclusionResult>(transcript, _jsonSerializerOptions.SerializerOptions);

                    if (result.Result is not null)
                    {
                        OmnichannelActivity omnichannelActivity = null;

                        if (campaign.AllowAIToUpdateSubject && result.Result.Subject is not null)
                        {
                            subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                            subject.Merge(result.Result.Subject);

                            omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                            omnichannelActivity.Subject = subject;

                            // Update the activity with the new subject since the converation may not be concluded.
                            await _omnichannelActivityStore.UpdateAsync(omnichannelActivity);
                        }

                        if (campaign.AllowAIToUpdateContact && result.Result.Contact is not null)
                        {
                            contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                            if (contact is not null)
                            {
                                contact.Merge(result.Result.Contact);

                                await contentManager.UpdateAsync(contact);
                            }
                        }

                        if (result.Result.Concluded)
                        {
                            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                            var workflowManager = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();

                            omnichannelActivity ??= await store.FindByIdAsync(activity.ItemId);

                            omnichannelActivity.Status = ActivityStatus.Completed;
                            omnichannelActivity.CompletedUtc = clock.UtcNow;
                            omnichannelActivity.DispositionId = result.Result.DispositionId;
                            omnichannelActivity.CompletedById = omnichannelActivity.AssignedToId;
                            omnichannelActivity.CompletedByUsername = omnichannelActivity.AssignedToUsername;

                            await _omnichannelActivityStore.UpdateAsync(omnichannelActivity);

                            var disposition = await _omnichannelActivityStore.FindByIdAsync(activity.DispositionId);

                            subject ??= activity.Subject ?? await contentManager.NewAsync(activity.SubjectContentType);
                            contact ??= await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

                            var input = new Dictionary<string, object>
                            {
                                { "Activity", activity },
                                { "Contact", contact },
                                { "Subject", subject },
                                { "Disposition", disposition },
                            };

                            await workflowManager.TriggerEventAsync(nameof(CompletedActivityEvent), input, correlationId: activity.ItemId);
                        }
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

        public ContentItem Subject { get; set; }

        public ContentItem Contact { get; set; }
    }
}
