using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An in-memory <see cref="IContactCenterWorkStateService"/> that behaves like the production service:
/// it owns the routing state and reconciles the CRM activity that projects it. Using a behaving double
/// rather than a strict mock keeps the existing assertions on the projected activity honest, because a
/// mock would let routing stop writing work state entirely without a single test noticing.
/// </summary>
internal sealed class FakeContactCenterWorkStateService : IContactCenterWorkStateService
{
    private readonly Dictionary<string, ContactCenterWorkState> _states = new(StringComparer.Ordinal);
    private readonly IOmnichannelActivityManager _activityManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeContactCenterWorkStateService"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to reconcile the projected read model, if any.</param>
    public FakeContactCenterWorkStateService(IOmnichannelActivityManager activityManager = null)
    {
        _activityManager = activityManager;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterWorkState> GetAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        if (_states.TryGetValue(activityItemId, out var workState))
        {
            return workState;
        }

        return await AdoptAsync(activityItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterWorkState> MutateAsync(
        string activityItemId,
        Action<ContactCenterWorkState> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        if (!_states.TryGetValue(activityItemId, out var workState))
        {
            workState = await AdoptAsync(activityItemId, cancellationToken) ?? new ContactCenterWorkState
            {
                ItemId = "work-state-" + activityItemId,
                ActivityItemId = activityItemId,
            };

            _states[activityItemId] = workState;
        }

        mutate(workState);

        if (_activityManager is not null)
        {
            var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

            if (activity is not null)
            {
                ContactCenterWorkStateProjector.Apply(activity, workState);
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
            }
        }

        return workState;
    }

    private async Task<ContactCenterWorkState> AdoptAsync(string activityItemId, CancellationToken cancellationToken)
    {
        if (_activityManager is null)
        {
            return null;
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return null;
        }

        var adopted = new ContactCenterWorkState
        {
            ItemId = "work-state-" + activityItemId,
            ActivityItemId = activityItemId,
        };

        ContactCenterWorkStateProjector.SeedFromActivity(adopted, activity);

        return adopted;
    }

    /// <summary>
    /// Seeds a work state directly, for tests that need routing to start from an existing state.
    /// </summary>
    /// <param name="workState">The work state to seed.</param>
    public void Seed(ContactCenterWorkState workState)
    {
        ArgumentNullException.ThrowIfNull(workState);

        _states[workState.ActivityItemId] = workState;
    }

    /// <summary>
    /// Seeds a work state from the projected fields an activity already carries, so that a test which
    /// arranges the activity keeps arranging routing state with it.
    /// </summary>
    /// <param name="activity">The activity whose projected fields describe the desired routing state.</param>
    public void SeedFrom(OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        _states[activity.ItemId] = new ContactCenterWorkState
        {
            ItemId = "work-state-" + activity.ItemId,
            ActivityItemId = activity.ItemId,
            AssignmentStatus = activity.AssignmentStatus,
            ReservationId = activity.ReservationId,
            ReservedById = activity.ReservedById,
            ReservedByUsername = activity.ReservedByUsername,
            ReservedUtc = activity.ReservedUtc,
            ReservationExpiresUtc = activity.ReservationExpiresUtc,
            AssignedToId = activity.AssignedToId,
            AssignedToUsername = activity.AssignedToUsername,
            AssignedToUtc = activity.AssignedToUtc,
            Attempts = activity.Attempts,
        };
    }
}
