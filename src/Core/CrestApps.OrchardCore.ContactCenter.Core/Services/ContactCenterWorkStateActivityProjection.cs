using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterWorkStateActivityProjection"/>.
/// </summary>
public sealed class ContactCenterWorkStateActivityProjection : IContactCenterWorkStateActivityProjection
{
    private const int MaxProjectionAttempts = 3;

    private readonly IContactCenterWorkStateManager _workStateManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateActivityProjection"/> class.
    /// </summary>
    /// <param name="workStateManager">The work state manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="scopeExecutor">The executor used to retry the projection in a fresh scope.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterWorkStateActivityProjection(
        IContactCenterWorkStateManager workStateManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterScopeExecutor scopeExecutor,
        ILogger<ContactCenterWorkStateActivityProjection> logger)
    {
        _workStateManager = workStateManager;
        _activityManager = activityManager;
        _scopeExecutor = scopeExecutor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProjectAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        try
        {
            await ProjectCoreAsync(activityItemId, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await RetryInFreshScopeAsync(activityItemId, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TrySeedAsync(ContactCenterWorkState workState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workState);

        if (string.IsNullOrEmpty(workState.ActivityItemId))
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(workState.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            return false;
        }

        ContactCenterWorkStateProjector.SeedFromActivity(workState, activity);

        return true;
    }

    private async Task ProjectCoreAsync(string activityItemId, CancellationToken cancellationToken)
    {
        var workState = await _workStateManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (workState is null)
        {
            return;
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        if (!ContactCenterWorkStateProjector.HasDivergence(activity, workState))
        {
            return;
        }

        ContactCenterWorkStateProjector.Apply(activity, workState);

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
    }

    private async Task RetryInFreshScopeAsync(string activityItemId, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 2; attempt <= MaxProjectionAttempts; attempt++)
        {
            try
            {
                await _scopeExecutor.ExecuteAsync<IContactCenterWorkStateActivityProjection>(projection =>
                    projection.ProjectAsync(activityItemId, cancellationToken));

                return;
            }
            catch (ConcurrencyException exception)
            {
                lastException = exception;
            }
        }

        // The CRM activity is a read model for this data; work state remains authoritative and the next
        // routing transition re-schedules the projection, so a losing race is logged rather than thrown.
        _logger.LogWarning(
            lastException,
            "Unable to project Contact Center work state onto the CRM activity '{ActivityItemId}' after {Attempts} attempts.",
            activityItemId.SanitizeLogValue(),
            MaxProjectionAttempts);
    }
}
