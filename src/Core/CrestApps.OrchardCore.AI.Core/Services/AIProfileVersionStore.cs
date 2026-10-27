using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps the saved versions of AI profiles.
/// </summary>
public interface IAIProfileVersionStore
{
    /// <summary>
    /// Saves a version.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(AIProfileVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a version by its identifier.
    /// </summary>
    /// <param name="itemId">The version's identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<AIProfileVersion> FindByIdAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the highest version number of a profile, or zero when it has none.
    /// </summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> GetLatestVersionAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of a profile's versions, newest first, and how many it has.
    /// </summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The number of versions per page.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<(IReadOnlyList<AIProfileVersion> Versions, int Count)> PageAsync(string profileId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps AI profile versions in the AI collection.
/// </summary>
public sealed class YesSqlAIProfileVersionStore : IAIProfileVersionStore
{
    private readonly ISession _session;
    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlAIProfileVersionStore"/> class.
    /// </summary>
    /// <param name="session">The document session.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public YesSqlAIProfileVersionStore(ISession session, IOptions<YesSqlStoreOptions> options)
    {
        _session = session;
        _collectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(AIProfileVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        await _session.SaveAsync(version, false, _collectionName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIProfileVersion> FindByIdAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        return await _session
            .Query<AIProfileVersion, AIProfileVersionIndex>(index => index.ItemId == itemId, _collectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetLatestVersionAsync(string profileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(profileId))
        {
            return 0;
        }

        var latest = await _session
            .QueryIndex<AIProfileVersionIndex>(index => index.ProfileId == profileId, _collectionName)
            .OrderByDescending(index => index.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return latest?.Version ?? 0;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AIProfileVersion> Versions, int Count)> PageAsync(string profileId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AIProfileVersion, AIProfileVersionIndex>(index => index.ProfileId == profileId, _collectionName);

        var count = await query.CountAsync(cancellationToken);
        var versions = await query
            .OrderByDescending(index => index.Version)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return (versions.ToList(), count);
    }
}
