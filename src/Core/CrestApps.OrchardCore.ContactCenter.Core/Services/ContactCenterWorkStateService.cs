using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterWorkStateService"/>.
/// </summary>
public sealed class ContactCenterWorkStateService : IContactCenterWorkStateService
{
    private readonly IContactCenterWorkStateManager _workStateManager;
    private readonly IContactCenterWorkStateActivityProjection _activityProjection;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateService"/> class.
    /// </summary>
    /// <param name="workStateManager">The work state manager.</param>
    /// <param name="activityProjections">The optional CRM projection, which is absent when CRM activity management is not enabled.</param>
    /// <param name="scopeExecutor">The executor used to project work state to the CRM after commit.</param>
    /// <param name="clock">The clock used to stamp work state times.</param>
    public ContactCenterWorkStateService(
        IContactCenterWorkStateManager workStateManager,
        IEnumerable<IContactCenterWorkStateActivityProjection> activityProjections,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock)
    {
        _workStateManager = workStateManager;
        _activityProjection = activityProjections.FirstOrDefault();
        _scopeExecutor = scopeExecutor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterWorkState> GetAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return null;
        }

        var workState = await _workStateManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (workState is not null)
        {
            return workState;
        }

        // Work that was already in flight when this feature was upgraded has no work state document yet. Rather
        // than reporting it as unassigned with zero attempts — which would re-offer live work and defeat the
        // dialer's attempt cap — the pre-existing projection on the activity is adopted as the answer.
        if (_activityProjection is null)
        {
            return null;
        }

        var adopted = new ContactCenterWorkState
        {
            ActivityItemId = activityItemId,
        };

        if (!await _activityProjection.TrySeedAsync(adopted, cancellationToken))
        {
            return null;
        }

        return adopted;
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

        var workState = await _workStateManager.FindByActivityIdAsync(activityItemId, cancellationToken);
        var isNew = workState is null;

        if (isNew)
        {
            workState = await _workStateManager.NewAsync(cancellationToken: cancellationToken);
            workState.ActivityItemId = activityItemId;
            workState.CreatedUtc = _clock.UtcNow;

            if (_activityProjection is not null)
            {
                await _activityProjection.TrySeedAsync(workState, cancellationToken);
            }
        }

        mutate(workState);
        workState.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _workStateManager.CreateAsync(workState, cancellationToken: cancellationToken);
        }
        else
        {
            await _workStateManager.UpdateAsync(workState, cancellationToken: cancellationToken);
        }

        if (_activityProjection is not null &&
            !_scopeExecutor.ScheduleAfterCommit<IContactCenterWorkStateActivityProjection>(
                projection => projection.ProjectAsync(activityItemId, CancellationToken.None)))
        {
            await _activityProjection.ProjectAsync(activityItemId, cancellationToken);
        }

        return workState;
    }
}
