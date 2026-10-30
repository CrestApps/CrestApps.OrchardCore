using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps what the AI remembers about each contact.
/// </summary>
public interface IAIContactMemoryStore
{
    /// <summary>
    /// Gets a contact's memory, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="contactId">The contact's content item id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<AIContactMemory> FindByContactAsync(string contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a contact's memory.
    /// </summary>
    /// <param name="memory">The memory.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(AIContactMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a contact's memory.
    /// </summary>
    /// <param name="memory">The memory.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DeleteAsync(AIContactMemory memory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps contact memory in the AI collection.
/// </summary>
public sealed class YesSqlAIContactMemoryStore : IAIContactMemoryStore
{
    private readonly ISession _session;
    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlAIContactMemoryStore"/> class.
    /// </summary>
    /// <param name="session">The document session.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public YesSqlAIContactMemoryStore(ISession session, IOptions<YesSqlStoreOptions> options)
    {
        _session = session;
        _collectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<AIContactMemory> FindByContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contactId))
        {
            return null;
        }

        return await _session
            .Query<AIContactMemory, AIContactMemoryIndex>(index => index.ContactId == contactId, _collectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);

        await _session.SaveAsync(memory, false, _collectionName, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(AIContactMemory memory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);

        _session.Delete(memory, _collectionName);

        return Task.CompletedTask;
    }
}
