using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The store of credential awards.
/// </summary>
public sealed class CredentialAwardStore : DocumentCatalog<CredentialAward, CredentialAwardIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public CredentialAwardStore(ISession session)
        : base(session)
    {
        CollectionName = CredentialsConstants.CollectionName;
    }

    /// <summary>
    /// Finds an award by its public code.
    /// </summary>
    /// <param name="publicId">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The award, or <see langword="null"/>.</returns>
    public async Task<CredentialAward> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(publicId))
        {
            return null;
        }

        return await Session.Query<CredentialAward, CredentialAwardIndex>(index => index.PublicId == publicId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds the award a rule source made for a person.
    /// </summary>
    /// <param name="definitionId">The definition.</param>
    /// <param name="ruleKind">The rule kind.</param>
    /// <param name="sourceId">The source id.</param>
    /// <param name="userId">The recipient.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The award, or <see langword="null"/>.</returns>
    public async Task<CredentialAward> FindBySourceAsync(string definitionId, string ruleKind, string sourceId, string userId, CancellationToken cancellationToken = default)
        => await Session.Query<CredentialAward, CredentialAwardIndex>(index =>
                index.DefinitionId == definitionId &&
                index.RuleKind == ruleKind &&
                index.SourceId == sourceId &&
                index.RecipientUserId == userId,
                collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lists a person's awards, newest first.
    /// </summary>
    /// <param name="userId">The recipient.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The awards.</returns>
    public async Task<IReadOnlyList<CredentialAward>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        return (await Session.Query<CredentialAward, CredentialAwardIndex>(index => index.RecipientUserId == userId, collection: CollectionName)
            .OrderByDescending(index => index.IssuedUtc)
            .ThenByDescending(index => index.Id)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists a person's awards of one definition.
    /// </summary>
    /// <param name="userId">The recipient.</param>
    /// <param name="definitionId">The definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The awards.</returns>
    public async Task<IReadOnlyList<CredentialAward>> ListForUserAsync(string userId, string definitionId, CancellationToken cancellationToken = default)
        => (await Session.Query<CredentialAward, CredentialAwardIndex>(index => index.RecipientUserId == userId && index.DefinitionId == definitionId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the indexes of the revoked awards of a status list.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The indexes.</returns>
    public async Task<IReadOnlyList<int>> ListRevokedIndexesAsync(int listNumber, CancellationToken cancellationToken = default)
        => (await Session.QueryIndex<CredentialAwardIndex>(index => index.StatusListNumber == listNumber && index.Revoked, collection: CollectionName)
            .ListAsync(cancellationToken)).Select(index => index.StatusListIndex).ToArray();

    /// <summary>
    /// Tells whether a status list slot is taken.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="listIndex">The index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when an award holds it.</returns>
    public async Task<bool> IsSlotTakenAsync(int listNumber, int listIndex, CancellationToken cancellationToken = default)
        => await Session.QueryIndex<CredentialAwardIndex>(index => index.StatusListNumber == listNumber && index.StatusListIndex == listIndex, collection: CollectionName)
            .CountAsync(cancellationToken) > 0;

    /// <summary>
    /// Counts the awards in a status list.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    public Task<int> CountInListAsync(int listNumber, CancellationToken cancellationToken = default)
        => Session.QueryIndex<CredentialAwardIndex>(index => index.StatusListNumber == listNumber, collection: CollectionName)
            .CountAsync(cancellationToken);

    /// <summary>
    /// Gets the highest status list number in use, or 0 when there is no award.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number.</returns>
    public async Task<int> GetLastListNumberAsync(CancellationToken cancellationToken = default)
        => (await Session.QueryIndex<CredentialAwardIndex>(collection: CollectionName)
            .OrderByDescending(index => index.StatusListNumber)
            .FirstOrDefaultAsync(cancellationToken))?.StatusListNumber ?? 0;

    /// <summary>
    /// Tells whether a status list exists.
    /// </summary>
    /// <param name="listNumber">The list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when an award is in it.</returns>
    public async Task<bool> ListExistsAsync(int listNumber, CancellationToken cancellationToken = default)
        => await CountInListAsync(listNumber, cancellationToken) > 0;

    /// <summary>
    /// Pages the awards for the admin list, newest first.
    /// </summary>
    /// <param name="filter">The filters.</param>
    /// <param name="utcNow">The current moment, for the expired and active states.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count of matching awards and the page.</returns>
    public async Task<(int Count, IReadOnlyList<CredentialAward> Items)> PageForAdminAsync(CredentialAwardAdminFilter filter, DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Session.Query<CredentialAward, CredentialAwardIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(filter.DefinitionId))
        {
            query = query.Where(index => index.DefinitionId == filter.DefinitionId);
        }

        if (!string.IsNullOrEmpty(filter.RecipientUserId))
        {
            query = query.Where(index => index.RecipientUserId == filter.RecipientUserId);
        }

        if (filter.IssuedFromUtc is { } from)
        {
            query = query.Where(index => index.IssuedUtc >= from);
        }

        if (filter.IssuedToUtc is { } to)
        {
            query = query.Where(index => index.IssuedUtc < to);
        }

        query = filter.Status switch
        {
            CredentialAwardStatus.Revoked => query.Where(index => index.Revoked),
            CredentialAwardStatus.Expired => query.Where(index => !index.Revoked && index.ExpiresUtc != null && index.ExpiresUtc <= utcNow),
            CredentialAwardStatus.Active => query.Where(index => !index.Revoked && (index.ExpiresUtc == null || index.ExpiresUtc > utcNow)),
            _ => query,
        };

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.IssuedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Pages every award in the order they were stored, for exports and data sets.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<CredentialAward>> PageAllAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (await Session.Query<CredentialAward, CredentialAwardIndex>(collection: CollectionName)
            .OrderBy(index => index.Id)
            .Skip(skip)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
}
