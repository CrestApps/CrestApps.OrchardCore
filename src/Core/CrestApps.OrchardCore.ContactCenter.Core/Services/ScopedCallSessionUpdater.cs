using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="ICallSessionUpdater"/>: each change runs in a child scope of its own, on a fresh read,
/// and is retried when the commit loses to another writer.
/// </summary>
public sealed class ScopedCallSessionUpdater : ICallSessionUpdater
{
    // A webhook burst on one call (a bridge, its unhold, its answered) is a few writes; more attempts than that is a loop.
    private const int MaxAttempts = 4;

    private readonly IContactCenterScopeExecutor _scopeExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedCallSessionUpdater"/> class.
    /// </summary>
    public ScopedCallSessionUpdater(IContactCenterScopeExecutor scopeExecutor)
    {
        _scopeExecutor = scopeExecutor;
    }

    /// <inheritdoc/>
    public Task<bool> UpdateAsync(string interactionId, Func<CallSession, bool> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        return RunAsync(interactionId, (session, _) => mutate(session), includeInteraction: false, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateWithInteractionAsync(string interactionId, Func<CallSession, Interaction, bool> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        return RunAsync(interactionId, mutate, includeInteraction: true, cancellationToken);
    }

    private async Task<bool> RunAsync(
        string interactionId,
        Func<CallSession, Interaction, bool> mutate,
        bool includeInteraction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        for (var attempt = 1; ; attempt++)
        {
            var saved = false;

            try
            {
                await _scopeExecutor.ExecuteAsync(async services =>
                {
                    var sessions = services.GetRequiredService<ICallSessionManager>();
                    var session = await sessions.FindByInteractionIdAsync(interactionId, cancellationToken);
                    Interaction interaction = null;

                    if (includeInteraction)
                    {
                        interaction = await services.GetRequiredService<IInteractionManager>().FindByIdAsync(interactionId, cancellationToken);
                    }

                    if (session is null || (includeInteraction && interaction is null) || !mutate(session, interaction))
                    {
                        return;
                    }

                    await sessions.UpdateAsync(session, cancellationToken: cancellationToken);

                    if (includeInteraction)
                    {
                        await services.GetRequiredService<IInteractionManager>().UpdateAsync(interaction, cancellationToken: cancellationToken);
                    }

                    saved = true;
                });

                return saved;
            }
            catch (ConcurrencyException) when (attempt < MaxAttempts)
            {
                // Somebody else wrote the call; read it again and apply the change to what they wrote.
            }
        }
    }
}
