using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterActivityWriter"/>.
/// </summary>
public sealed class ContactCenterActivityWriter : IContactCenterActivityWriter
{
    private const int MaxActivityWriteAttempts = 3;

    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityWriter"/> class.
    /// </summary>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="scopeExecutor">The executor used to defer and retry the write outside the routing scope.</param>
    /// <param name="session">The YesSql session used to commit the write on its own.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterActivityWriter(
        IOmnichannelActivityManager activityManager,
        IContactCenterScopeExecutor scopeExecutor,
        ISession session,
        ILogger<ContactCenterActivityWriter> logger)
    {
        _activityManager = activityManager;
        _scopeExecutor = scopeExecutor;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task ScheduleUpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (string.IsNullOrEmpty(activityItemId))
        {
            return Task.CompletedTask;
        }

        if (_scopeExecutor.ScheduleAfterCommit<IContactCenterActivityWriter>(
            writer => writer.UpdateAsync(activityItemId, mutate, CancellationToken.None)))
        {
            return Task.CompletedTask;
        }

        // Without a shell scope there is nothing to defer to, so the mutation joins the caller's session and
        // is committed by whoever owns it, exactly as it was before the write was moved out of routing.
        return ApplyAsync(activityItemId, mutate, save: false, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        try
        {
            await ApplyAsync(activityItemId, mutate, save: true, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await RetryInFreshScopeAsync(activityItemId, mutate, cancellationToken);
        }
    }

    private async Task ApplyAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        bool save,
        CancellationToken cancellationToken)
    {
        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        mutate(activity);

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

        if (save)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RetryInFreshScopeAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 2; attempt <= MaxActivityWriteAttempts; attempt++)
        {
            try
            {
                await _scopeExecutor.ExecuteAsync<IContactCenterActivityWriter>(writer =>
                    writer.UpdateAsync(activityItemId, mutate, cancellationToken));

                return;
            }
            catch (ConcurrencyException exception)
            {
                lastException = exception;
            }
        }

        _logger.LogWarning(
            "Unable to apply a Contact Center write to the CRM activity '{ActivityItemId}' after {Attempts} attempts. {Error}",
            OperationalLogRedactor.Pseudonymize(activityItemId, OperationalLogIdentifierCategory.Activity),
            MaxActivityWriteAttempts,
            OperationalLogRedactor.RedactException(lastException));
    }
}
