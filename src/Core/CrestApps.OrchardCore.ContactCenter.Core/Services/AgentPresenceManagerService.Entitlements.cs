using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The manager-owned half of the presence manager: entitlements and portable configuration, which change what an
/// agent may sign in to but never the state the agent is in.
/// </summary>
public sealed partial class AgentPresenceManagerService
{
    /// <inheritdoc/>
    public Task<AgentProfile> UpdateEntitlementsAsync(
        string agentId,
        AgentEntitlements entitlements,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentNullException.ThrowIfNull(entitlements);

        return UpdateManagedConfigurationCoreAsync(
            agentId,
            entitlements.AllowedQueueIds,
            entitlements.AllowedCampaignIds,
            profile =>
            {
                // The entitlement screen is the one place skills are edited, so what it sends is the whole
                // set: the tag list is derived from the proficiencies rather than kept alongside them.
                AgentEntitlementUtilities.ApplySkills(profile, skills: null, entitlements.SkillProficiencies ?? []);
                profile.QueueMemberships = AgentEntitlementUtilities.NormalizeQueueMemberships(entitlements.QueueMemberships);
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AgentProfile> ApplyManagedConfigurationAsync(
        string agentId,
        AgentManagedConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentNullException.ThrowIfNull(configuration);

        return UpdateManagedConfigurationCoreAsync(
            agentId,
            configuration.AllowedQueueIds,
            configuration.AllowedCampaignIds,
            profile =>
            {
                profile.DisplayName = configuration.DisplayName;
                profile.MaxConcurrentInteractions = configuration.MaxConcurrentInteractions;
                AgentEntitlementUtilities.ApplySkills(profile, configuration.Skills, configuration.SkillProficiencies);

                if (configuration.QueueMemberships is not null)
                {
                    profile.QueueMemberships = AgentEntitlementUtilities.NormalizeQueueMemberships(configuration.QueueMemberships);
                }
            },
            cancellationToken);
    }

    private async Task<AgentProfile> UpdateManagedConfigurationCoreAsync(
        string agentId,
        IEnumerable<string> allowedQueueIds,
        IEnumerable<string> allowedCampaignIds,
        Action<AgentProfile> applyAdditionalConfiguration,
        CancellationToken cancellationToken)
    {
        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(profile.UserId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{profile.UserId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds(allowedQueueIds);
        profile.AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds(allowedCampaignIds);

        applyAdditionalConfiguration?.Invoke(profile);

        var previousQueueIds = profile.QueueIds.ToList();
        var previousCampaignIds = profile.CampaignIds.ToList();
        var prunedQueueIds = AgentEntitlementUtilities.FilterEntitled(profile.QueueIds, profile.AllowedQueueIds);
        var prunedCampaignIds = AgentEntitlementUtilities.FilterEntitled(profile.CampaignIds, profile.AllowedCampaignIds);

        var membershipChanged = !prunedQueueIds.SequenceEqual(profile.QueueIds, StringComparer.OrdinalIgnoreCase) ||
            !prunedCampaignIds.SequenceEqual(profile.CampaignIds, StringComparer.OrdinalIgnoreCase);

        profile.QueueIds = prunedQueueIds;
        profile.CampaignIds = prunedCampaignIds;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        var removedQueueIds = previousQueueIds
            .Except(profile.QueueIds, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removedCampaignIds = previousCampaignIds
            .Except(profile.CampaignIds, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await PublishEntitlementsChangedAsync(
            profile,
            removedQueueIds,
            removedCampaignIds,
            cancellationToken);

        if (membershipChanged)
        {
            await SyncSessionMembershipAsync(profile.UserId, profile.QueueIds, profile.CampaignIds, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Pruned unauthorized Contact Center live queue or campaign membership for agent '{AgentId}' after manager entitlement changes.",
                    profile.ItemId.SanitizeLogValue());
            }
        }

        return profile;
    }

    private Task PublishEntitlementsChangedAsync(
        AgentProfile profile,
        IEnumerable<string> removedQueueIds,
        IEnumerable<string> removedCampaignIds,
        CancellationToken cancellationToken)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentEntitlementsChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = profile.ItemId,
            ActorId = ContactCenterConstants.SystemActor,
            SourceComponent = ContactCenterConstants.Components.Agents,
        };

        interactionEvent.SetData(new AgentEntitlementsChangedEventData
        {
            AllowedQueueIds = profile.AllowedQueueIds.ToList(),
            AllowedCampaignIds = profile.AllowedCampaignIds.ToList(),
            RemovedQueueIds = removedQueueIds.ToList(),
            RemovedCampaignIds = removedCampaignIds.ToList(),
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
