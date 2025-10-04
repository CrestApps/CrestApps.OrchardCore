using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Indexes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const int _maxAttempts = 5;

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAICompletionService _aICompletionService;
    private readonly IAIProfileManager _aIProfileManager;
    private readonly ISession _session;
    private readonly ISmsService _smsService;
    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly ILogger _logger;

    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAICompletionService aICompletionService,
        IAIProfileManager aIProfileManager,
        ISession session,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        ILogger<SmsOmnichannelEventHandler> logger)
    {
        _chatSessionManager = chatSessionManager;
        _aICompletionService = aICompletionService;
        _aIProfileManager = aIProfileManager;
        _session = session;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;
        _logger = logger;
    }

    public async Task HandleAsync(OmnichannelEvent omnichannelEvent)
    {
        if (omnichannelEvent.EventType != OmnichannelConstants.Events.SmsReceived &&
            omnichannelEvent.Message.Channel == OmnichannelConstants.Channels.Sms &&
            omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        var activity = await _omnichannelActivityStore.GetAsync(omnichannelEvent.Message.Channel,
            omnichannelEvent.Message.ServiceAddress,
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

        if (string.IsNullOrWhiteSpace(activity.AIProfileName))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an AI Profile associated with it. Cannot process incoming SMS message.", activity.ItemId);

            return;
        }

        var chatSession = await _session.Query<AIChatSession, OminchannelActivityAIChatSessionIndex>(index => index.ActivityId == activity.ItemId).FirstOrDefaultAsync();

        var aiProfile = await _aIProfileManager.FindByIdAsync(activity.AIProfileName);

        if (aiProfile == null)
        {
            _logger.LogWarning("The AI Profile {AIProfileId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.AIProfileName, activity.ItemId);

            if (chatSession is not null)
            {
                // Mark the chat session as closed.
                chatSession.Prompts.Add(new AIChatSessionPrompt
                {
                    Id = IdGenerator.GenerateId(),
                    Role = ChatRole.User,
                    Content = omnichannelEvent.Message.Content
                });

                await _session.SaveAsync(chatSession);
            }

            return;
        }

        chatSession ??= await _chatSessionManager.NewAsync(aiProfile, NewAIChatSessionContext.Robots);

        chatSession.Prompts.Add(new AIChatSessionPrompt
        {
            Id = IdGenerator.GenerateId(),
            Role = ChatRole.User,
            Content = omnichannelEvent.Message.Content
        });

        var aiAttempts = 0;

        string bestChoice = null;

        while (aiAttempts++ < _maxAttempts)
        {
            try
            {
                var transcript = chatSession.Prompts.Where(x => !x.IsGeneratedPrompt)
                    .Select(prompt => new ChatMessage(prompt.Role, prompt.Content));

                // Here we need to return an object that gives us IsCompleted, a Disposition or and a message.
                var completion = await _aICompletionService.CompleteAsync(aiProfile.Source, transcript, new AICompletionContext
                {
                    Profile = aiProfile,
                    Session = chatSession,
                });

                bestChoice = completion?.Messages?.FirstOrDefault()?.Text;

                if (string.IsNullOrWhiteSpace(bestChoice))
                {
                    _logger.LogWarning("AI Completion did not return any content for Activity {ActivityId} using AI Profile {AIProfileId}.", activity.ItemId, aiProfile.ItemId);

                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Completion failed for Activity {ActivityId} using AI Profile {AIProfileId}.", activity.ItemId, aiProfile.ItemId);
            }
        }

        try
        {
            var smsAttempt = 0;

            while (smsAttempt++ < _maxAttempts)
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

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS message to {To} for Activity {ActivityId}.", activity.PreferredDestination, activity.ItemId);
        }

        await _session.SaveAsync(chatSession);
    }
}
