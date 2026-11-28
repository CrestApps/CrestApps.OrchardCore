using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Describes a saved queue for <c>saveContactCenterQueue</c>, from an explicit list of fields.
/// </summary>
internal sealed class ContactCenterQueueDescriber
{
    private readonly ILocalClock _localClock;
    private readonly Dictionary<string, string> _queueNames;
    private readonly Dictionary<string, string> _groupNames;
    private readonly Dictionary<string, string> _calendarNames;

    private ContactCenterQueueDescriber(
        ILocalClock localClock,
        Dictionary<string, string> queueNames,
        Dictionary<string, string> groupNames,
        Dictionary<string, string> calendarNames)
    {
        _localClock = localClock;
        _queueNames = queueNames;
        _groupNames = groupNames;
        _calendarNames = calendarNames;
    }

    /// <summary>
    /// Reads the names a queue refers to.
    /// </summary>
    public static async Task<ContactCenterQueueDescriber> CreateAsync(IServiceProvider services, CancellationToken cancellationToken)
        => new(
            services.GetService<ILocalClock>(),
            await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken),
            await ContactCenterSetupLookups.GetQueueGroupNamesAsync(services, cancellationToken),
            await ContactCenterSetupLookups.GetCalendarNamesAsync(services, cancellationToken));

    /// <summary>
    /// Gets the name of a queue.
    /// </summary>
    public string QueueName(string id)
        => id is not null && _queueNames.TryGetValue(id, out var name) ? name : null;

    /// <summary>
    /// Describes a queue in full, with the names of the records it refers to, for the result of a save.
    /// </summary>
    public async Task<object> DescribeAsync(ActivityQueue queue)
    {
        var treatment = queue.Treatment ?? new QueueTreatmentSettings();

        return new
        {
            id = queue.ItemId,
            name = queue.Name,
            description = queue.Description,
            enabled = queue.Enabled,
            group = ContactCenterSetupLookups.Reference(queue.QueueGroupId, _groupNames),
            defaultPriority = queue.DefaultPriority.ToString(),
            routingStrategy = queue.RoutingStrategy.ToString(),
            preferStickyAgent = queue.PreferStickyAgent,
            enableSlaAging = queue.EnableSlaAging,
            slaThresholdSeconds = queue.SlaThresholdSeconds,
            firstResponseTargetSeconds = queue.FirstResponseTargetSeconds,
            reservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            unansweredOfferAction = queue.UnansweredOfferAction.ToString(),
            businessHoursCalendar = ContactCenterSetupLookups.Reference(queue.BusinessHoursCalendarId, _calendarNames),
            afterHoursAction = queue.AfterHoursAction.ToString(),
            inboundChannelEndpointId = queue.InboundChannelEndpointId,
            requiredSkills = queue.RequiredSkills ?? [],
            skillRequirements = (queue.SkillRequirements ?? [])
                .Where(requirement => requirement is not null)
                .Select(requirement => new
                {
                    skillId = requirement.SkillId,
                    minimumProficiency = requirement.MinimumProficiency,
                    required = requirement.Required,
                    relaxAfterSeconds = requirement.RelaxAfterSeconds,
                })
                .ToList(),
            overflowQueue = ContactCenterSetupLookups.Reference(queue.OverflowQueueId, _queueNames),
            overflowAfterSeconds = queue.OverflowAfterSeconds,
            overflowTargets = (queue.OverflowTargets ?? [])
                .Where(target => target is not null)
                .Select(target => new
                {
                    queueId = target.QueueId,
                    queueName = QueueName(target.QueueId),
                    afterSeconds = target.AfterSeconds,
                })
                .ToList(),
            maxWaitSeconds = queue.MaxWaitSeconds,
            maxWaitAction = queue.MaxWaitAction.ToString(),
            maxQueueSize = queue.MaxQueueSize,
            queueFullAction = queue.QueueFullAction.ToString(),
            treatment = new
            {
                welcomeMessage = treatment.WelcomeMessage,
                announcementIntervalSeconds = treatment.AnnouncementIntervalSeconds,
                announcePosition = treatment.AnnouncePosition,
                announceEstimatedWait = treatment.AnnounceEstimatedWait,
                holdMusicMediaId = treatment.HoldMusicMediaId,
                callbackDtmfKey = treatment.CallbackDtmfKey,
                callbackOfferAfterSeconds = treatment.CallbackOfferAfterSeconds,
                minimumEstimateSeconds = treatment.MinimumEstimateSeconds,
                maximumEstimateSeconds = treatment.MaximumEstimateSeconds,
                averageHandleTimeSeconds = treatment.AverageHandleTimeSeconds,
            },
            created = await ContactCenterSetupToolBase.ToLocalAsync(_localClock, queue.CreatedUtc),
            modified = await ContactCenterSetupToolBase.ToLocalAsync(_localClock, queue.ModifiedUtc),
        };
    }
}
