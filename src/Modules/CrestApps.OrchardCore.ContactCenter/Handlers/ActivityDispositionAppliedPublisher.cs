using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Publishes <c>ActivityDispositionApplied</c> whenever an activity is completed with a disposition, whoever applied
/// it, so a workflow can follow up on it.
/// </summary>
/// <remarks>
/// The event was offered to workflows and never published, so a workflow waiting on it never ran. It is published for
/// every completion: an agent's, the dialer's when it finds a number not in service, and an automated call's, with the
/// disposition's outcome and the contact's number, which is what a follow-up such as a text message needs.
/// </remarks>
public sealed class ActivityDispositionAppliedPublisher : IActivityDispositionHandler
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityDispositionAppliedPublisher"/> class.
    /// </summary>
    /// <param name="dispositionsCatalog">The dispositions, for the applied disposition's name and outcome.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="interactionManager">The interactions, read for the dialer attempt the activity was completed after.</param>
    /// <param name="agentManager">The agent profiles, read for the user of the agent who took or was held for the call.</param>
    /// <param name="logger">The logger.</param>
    public ActivityDispositionAppliedPublisher(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IContactCenterEventPublisher publisher,
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        ILogger<ActivityDispositionAppliedPublisher> logger)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _publisher = publisher;
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DispositionedAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null || activity.Status != ActivityStatus.Completed)
        {
            return;
        }

        var disposition = string.IsNullOrEmpty(activity.DispositionId)
            ? null
            : await _dispositionsCatalog.FindByIdAsync(activity.DispositionId, cancellationToken);

        var data = new ActivityDispositionEventData
        {
            ActivityItemId = activity.ItemId,
            DispositionId = activity.DispositionId,
            DispositionName = disposition?.Name,
            Outcome = (disposition?.Outcome ?? DispositionOutcome.None).ToString(),
            Source = request.Source.ToString(),
            TerminalReasonCode = activity.TerminalReasonCode,
            Channel = activity.Channel,
            CampaignId = activity.CampaignId,
            SubjectContentType = activity.SubjectContentType,
            ContactContentItemId = activity.ContactContentItemId,
            PhoneNumber = activity.PreferredDestination,
            Attempts = activity.Attempts,
            CompletedById = activity.CompletedById,
        };

        // What a workflow needs to decide whether, and when, the dialer calls again: which attempt this was, how many
        // the profile allows, and how the call ended.
        var interaction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (interaction is not null)
        {
            data.InteractionId = interaction.ItemId;

            if (DialerCallMetadata.IsCampaignDial(interaction))
            {
                data.DialerProfileId = DialerCallMetadata.GetDialerProfileId(interaction);
                data.DialerOutcome = DialerCallMetadata.GetOutcome(interaction);
                data.AttemptNumber = DialerCallMetadata.GetAttemptNumber(interaction) ?? Math.Max(1, activity.Attempts);
                data.MaxAttempts = DialerCallMetadata.GetMaxAttempts(interaction);
                data.RemainingAttempts = data.MaxAttempts.HasValue
                    ? Math.Max(0, data.MaxAttempts.Value - data.AttemptNumber.Value)
                    : null;
            }

            // On an automated dial the agent held for the call stays on the interaction when nobody answers, so the
            // follow-up of a disposition the system applied still belongs to them.
            if (!string.IsNullOrEmpty(interaction.AgentId))
            {
                var agent = await _agentManager.FindByIdAsync(interaction.AgentId, cancellationToken);
                data.AgentUserId = agent?.UserId;
            }
        }

        if (string.IsNullOrEmpty(data.AgentUserId) && request.Source == ActivityDispositionSource.Agent)
        {
            data.AgentUserId = activity.CompletedById;
        }

        var actor = request.Source switch
        {
            ActivityDispositionSource.Agent when !string.IsNullOrEmpty(request.ActorId) => new ContactCenterActor(ContactCenterActorType.Agent, request.ActorId),
            ActivityDispositionSource.AI => new ContactCenterActor(ContactCenterActorType.AiAgent, null),
            ActivityDispositionSource.Provider => new ContactCenterActor(ContactCenterActorType.Provider, null),
            ActivityDispositionSource.Workflow => new ContactCenterActor(ContactCenterActorType.Workflow, null),
            _ => ContactCenterActor.System,
        };

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.ActivityDispositionApplied,
            AggregateType = nameof(OmnichannelActivity),
            AggregateId = activity.ItemId,
            ActorId = actor.Id ?? ContactCenterConstants.SystemActor,
            ActorType = actor.Type,
            SourceComponent = nameof(ActivityDispositionAppliedPublisher),
            IdempotencyKey = ContactCenterClaimKeys.FitIdempotencyKey($"activity:{ContactCenterConstants.Events.ActivityDispositionApplied}:{activity.ItemId}:{activity.DispositionId}"),
        };

        interactionEvent.SetData(data);

        await _publisher.PublishAsync(interactionEvent, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Published ActivityDispositionApplied for activity '{ActivityId}': disposition '{Disposition}', outcome {Outcome}, source {Source}, dialer outcome {DialerOutcome}, attempt {AttemptNumber} of {MaxAttempts} ({RemainingAttempts} left).",
                activity.ItemId.SanitizeLogValue(),
                data.DispositionName.SanitizeLogValue(),
                data.Outcome,
                data.Source,
                data.DialerOutcome,
                data.AttemptNumber,
                data.MaxAttempts,
                data.RemainingAttempts);
        }
    }
}
