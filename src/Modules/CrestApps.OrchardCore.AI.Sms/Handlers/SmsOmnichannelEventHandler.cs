using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Sms.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.AI.Sms.Handlers;

internal sealed class SmsOmnichannelEventHandler : IOmnichannelEventHandler
{
    private const int _maxAttempts = 5;

    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAICompletionService _aICompletionService;
    private readonly IAIProfileManager _aIProfileManager;
    private readonly ISession _session;
    private readonly ISmsService _smsService;
    private readonly ILogger _logger;

    public SmsOmnichannelEventHandler(
        IAIChatSessionManager chatSessionManager,
        IAICompletionService aICompletionService,
        IAIProfileManager aIProfileManager,
        ISession session,
        ISmsService smsService,
        ILogger<SmsOmnichannelEventHandler> logger)
    {
        _chatSessionManager = chatSessionManager;
        _aICompletionService = aICompletionService;
        _aIProfileManager = aIProfileManager;
        _session = session;
        _smsService = smsService;
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

        var activity = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
            index.Channel == omnichannelEvent.Message.Channel &&
            index.ChannelEndpoint == omnichannelEvent.Message.ServiceAddress &&
            index.PreferredDestination == omnichannelEvent.Message.CustomerAddress, collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();

        if (activity is null)
        {
            _logger.LogWarning("Unable to link incoming SMS message from a customer to an Activity. Channel: {Channel}, Service Address: {ServiceAddress}, Customer Address: {CustomerAddress}", omnichannelEvent.Message.Channel, omnichannelEvent.Message.ServiceAddress, omnichannelEvent.Message.CustomerAddress);

            return;
        }

        // Always set the activity status to AwaitingAgentResponse when a new message is received from the customer to ensure we don't miss responding to them.

        activity.Status = ActivityStatus.AwaitingAgentResponse;

        await _session.SaveAsync(activity);

        if (string.IsNullOrWhiteSpace(activity.AIProfileName))
        {
            _logger.LogWarning("The linked Activity {ActivityId} does not have an AI Profile associated with it. Cannot process incoming SMS message.", activity.Id);

            return;
        }

        var chatSession = await _session.Query<AIChatSession, OminchannelActivityAIChatSessionIndex>(index => index.ActivityId == activity.Id).FirstOrDefaultAsync();

        var aiProfile = await _aIProfileManager.FindByIdAsync(activity.AIProfileName);

        if (aiProfile == null)
        {
            _logger.LogWarning("The AI Profile {AIProfileId} associated with Activity {ActivityId} was not found. Cannot process incoming SMS message.", activity.AIProfileName, activity.Id);

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

        chatSession ??= await _chatSessionManager.NewAsync(aiProfile);

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
                    _logger.LogWarning("AI Completion did not return any content for Activity {ActivityId} using AI Profile {AIProfileId}.", activity.Id, aiProfile.Id);

                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Completion failed for Activity {ActivityId} using AI Profile {AIProfileId}.", activity.Id, aiProfile.Id);
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

                    await _session.SaveAsync(activity);

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS message to {To} for Activity {ActivityId}.", activity.PreferredDestination, activity.Id);
        }

        await _session.SaveAsync(chatSession);
    }
}
