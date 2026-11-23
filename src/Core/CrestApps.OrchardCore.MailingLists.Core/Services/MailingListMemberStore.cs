using System.Globalization;
using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Stores mailing list members in the mailing lists collection, stamping when each was created and changed.
/// </summary>
public sealed class MailingListMemberStore : DocumentCatalog<MailingListMember, MailingListMemberIndex>, IMailingListMemberStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock that stamps the created and changed times.</param>
    public MailingListMemberStore(ISession session, IClock clock)
        : base(session, clock)
    {
        CollectionName = MailingListsConstants.CollectionName;
    }

    /// <inheritdoc/>
    async Task<MailingListMember> IMailingListMemberStore.FindByIdAsync(string memberId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(memberId) ? null : await FindByIdAsync(memberId, cancellationToken);

    /// <inheritdoc/>
    public async Task<MailingListMember> FindAsync(string listId, string normalizedEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(listId) || string.IsNullOrEmpty(normalizedEmail))
        {
            return null;
        }

        return await Session.Query<MailingListMember, MailingListMemberIndex>(index => index.ListId == listId && index.NormalizedEmail == normalizedEmail, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MailingListMember>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return [];
        }

        return [.. await Session.Query<MailingListMember, MailingListMemberIndex>(index => index.NormalizedEmail == normalizedEmail, collection: CollectionName).ListAsync(cancellationToken)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MailingListMember>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        return [.. await Session.Query<MailingListMember, MailingListMemberIndex>(index => index.UserId == userId, collection: CollectionName).ListAsync(cancellationToken)];
    }

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<MailingListMember> Members)> PageAsync(MailingListMemberQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var listId = query.ListId;
        IQuery<MailingListMember> documents = Session.Query<MailingListMember>(collection: CollectionName);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLowerInvariant();

        if (search is not null)
        {
            // A text can't seek: grouped by document, the database reads the matches and sorts them instead of walking
            // an index of the order looking for the few rows that hold the text.
            documents = documents.NoIndexOptimizations();
        }

        var members = documents.With<MailingListMemberIndex>(index => index.ListId == listId);

        if (query.Status is { } status)
        {
            members = members.Where(index => index.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            var source = query.Source.Trim();

            members = members.Where(index => index.Source == source);
        }

        if (query.NotSyncedOnly)
        {
            members = members.Where(index => index.SyncState != MailingListSyncState.Synced);
        }

        if (search is not null)
        {
            members = members.Where(index => index.NormalizedEmail.Contains(search));
        }

        var count = await members.CountAsync(cancellationToken);
        var page = await members
            .OrderByDescending(index => index.StatusChangedUtc)
            .ThenByDescending(index => index.DocumentId)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<MailingListMember> Members, string NextCursor)> ListSubscribedAsync(string listId, string cursor, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(listId))
        {
            return ([], null);
        }

        var size = Math.Max(1, take);
        var after = long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        // The rows are read in document order from the cursor on, so the database seeks the list's index and stops when
        // the page is full; the documents are then loaded by id.
        var rows = await Session.QueryIndex<MailingListMemberIndex>(index => index.ListId == listId && index.Status == MailingListMemberStatus.Subscribed && index.DocumentId > after, collection: CollectionName)
            .OrderBy(index => index.DocumentId)
            .Take(size)
            .ListAsync(cancellationToken);

        var ids = rows.Select(row => row.DocumentId).ToArray();

        if (ids.Length == 0)
        {
            return ([], null);
        }

        var members = await Session.GetAsync<MailingListMember>(ids, CollectionName, cancellationToken);
        var next = ids.Length < size ? null : ids.Max().ToString(CultureInfo.InvariantCulture);

        return ([.. members.Where(member => member is not null)], next);
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(string listId, MailingListMemberStatus status, CancellationToken cancellationToken = default)
        => Session.QueryIndex<MailingListMemberIndex>(index => index.ListId == listId && index.Status == status, collection: CollectionName).CountAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountNotSyncedAsync(string listId, CancellationToken cancellationToken = default)
        => Session.QueryIndex<MailingListMemberIndex>(index => index.ListId == listId && index.SyncState != MailingListSyncState.Synced, collection: CollectionName).CountAsync(cancellationToken);

    /// <inheritdoc/>
    async Task IMailingListMemberStore.CreateAsync(MailingListMember member, CancellationToken cancellationToken)
        => await CreateAsync(member, cancellationToken);

    /// <inheritdoc/>
    async Task IMailingListMemberStore.UpdateAsync(MailingListMember member, CancellationToken cancellationToken)
        => await UpdateAsync(member, cancellationToken);

    /// <inheritdoc/>
    async Task IMailingListMemberStore.DeleteAsync(MailingListMember member, CancellationToken cancellationToken)
        => await DeleteAsync(member, cancellationToken);

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);
}
