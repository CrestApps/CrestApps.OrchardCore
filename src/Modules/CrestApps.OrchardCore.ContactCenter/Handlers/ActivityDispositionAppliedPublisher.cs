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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityDispositionAppliedPublisher"/> class.
    /// </summary>
    /// <param name="dispositionsCatalog">The dispositions, for the applied disposition's name and outcome.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="logger">The logger.</param>
    public ActivityDispositionAppliedPublisher(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IContactCenterEventPublisher publisher,
        ILogger<ActivityDispositionAppliedPublisher> logger)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _publisher = publisher;
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
                "Published ActivityDispositionApplied for activity '{ActivityId}': disposition '{Disposition}', outcome {Outcome}, source {Source}.",
                activity.ItemId.SanitizeLogValue(),
                data.DispositionName.SanitizeLogValue(),
                data.Outcome,
                data.Source);
        }
    }
}
