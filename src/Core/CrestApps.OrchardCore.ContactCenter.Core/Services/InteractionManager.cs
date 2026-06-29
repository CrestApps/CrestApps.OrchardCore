using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IInteractionManager"/>.
/// </summary>
public sealed class InteractionManager : IInteractionManager
{
    private readonly IInteractionStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying interaction store.</param>
    /// <param name="clock">The clock used to stamp audit times.</param>
    public InteractionManager(
        IInteractionStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    public ValueTask<Interaction> NewAsync()
    {
        return ValueTask.FromResult(new Interaction
        {
            ItemId = IdGenerator.GenerateId(),
            CorrelationId = IdGenerator.GenerateId(),
            CreatedUtc = _clock.UtcNow,
        });
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        EnsureIdentity(interaction);

        if (interaction.CreatedUtc == default)
        {
            interaction.CreatedUtc = _clock.UtcNow;
        }

        await _store.CreateAsync(interaction, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask UpdateAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        EnsureIdentity(interaction);
        interaction.ModifiedUtc = _clock.UtcNow;

        await _store.UpdateAsync(interaction, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<Interaction> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.FindByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        return await _store.FindByActivityIdAsync(activityItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        return await _store.FindByCorrelationIdAsync(correlationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default)
    {
        return await _store.PageByStatusAsync(page, pageSize, status, cancellationToken);
    }

    private static void EnsureIdentity(Interaction interaction)
    {
        if (string.IsNullOrEmpty(interaction.ItemId))
        {
            interaction.ItemId = IdGenerator.GenerateId();
        }

        if (string.IsNullOrEmpty(interaction.CorrelationId))
        {
            interaction.CorrelationId = IdGenerator.GenerateId();
        }
    }
}
