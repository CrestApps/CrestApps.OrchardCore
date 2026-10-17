using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Persists AI Profile Builder sessions.
/// </summary>
public interface IProfileBuilderSessionStore : ICatalog<ProfileBuilderSession>
{
    /// <summary>
    /// Finds the session whose conversation is the specified chat interaction.
    /// </summary>
    /// <param name="chatInteractionId">The chat interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The session, or <see langword="null"/> when the interaction belongs to none.</returns>
    Task<ProfileBuilderSession> FindByChatInteractionIdAsync(string chatInteractionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists sessions, most recently changed first.
    /// </summary>
    /// <param name="ownerId">The owner to list for, or <see langword="null"/> to list every owner's sessions.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The sessions.</returns>
    Task<IReadOnlyCollection<ProfileBuilderSession>> ListAsync(string ownerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The YesSql implementation of <see cref="IProfileBuilderSessionStore"/>.
/// </summary>
internal sealed class ProfileBuilderSessionStore : DocumentCatalog<ProfileBuilderSession, ProfileBuilderSessionIndex>, IProfileBuilderSessionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ProfileBuilderSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ProfileBuilderStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ProfileBuilderSession> FindByChatInteractionIdAsync(string chatInteractionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        var session = await Session.Query<ProfileBuilderSession, ProfileBuilderSessionIndex>(
            index => index.ChatInteractionId == chatInteractionId,
            collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(session);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProfileBuilderSession>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(ownerId)
            ? Session.Query<ProfileBuilderSession, ProfileBuilderSessionIndex>(collection: CollectionName)
            : Session.Query<ProfileBuilderSession, ProfileBuilderSessionIndex>(index => index.OwnerId == ownerId, collection: CollectionName);

        var sessions = await query
            .OrderByDescending(index => index.ModifiedUtc)
            .ListAsync(cancellationToken);

        return await LoadedAsync(sessions);
    }
}
