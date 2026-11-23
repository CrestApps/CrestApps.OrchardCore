using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// Members kept in memory, for the member service tests.
/// </summary>
internal sealed class InMemoryMailingListMemberStore : IMailingListMemberStore
{
    private readonly Dictionary<string, MailingListMember> _members = new(StringComparer.Ordinal);

    public IReadOnlyCollection<MailingListMember> All
        => _members.Values;

    public int Commits { get; private set; }

    public Task<MailingListMember> FindByIdAsync(string memberId, CancellationToken cancellationToken = default)
        => Task.FromResult(memberId is not null && _members.TryGetValue(memberId, out var member) ? member : null);

    public Task<MailingListMember> FindAsync(string listId, string normalizedEmail, CancellationToken cancellationToken = default)
        => Task.FromResult(_members.Values.FirstOrDefault(member => member.ListId == listId && member.NormalizedEmail == normalizedEmail));

    public Task<IReadOnlyList<MailingListMember>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MailingListMember>>([.. _members.Values.Where(member => member.NormalizedEmail == normalizedEmail)]);

    public Task<IReadOnlyList<MailingListMember>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MailingListMember>>([.. _members.Values.Where(member => member.UserId == userId)]);

    public Task<(int Count, IReadOnlyList<MailingListMember> Members)> PageAsync(MailingListMemberQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var matches = _members.Values.Where(member => member.ListId == query.ListId).ToList();
        IReadOnlyList<MailingListMember> page = [.. matches.Skip(skip).Take(take)];

        return Task.FromResult((matches.Count, page));
    }

    public Task<(IReadOnlyList<MailingListMember> Members, string NextCursor)> ListSubscribedAsync(string listId, string cursor, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MailingListMember> members = [.. _members.Values.Where(member => member.ListId == listId && member.Status == MailingListMemberStatus.Subscribed)];

        return Task.FromResult<(IReadOnlyList<MailingListMember> Members, string NextCursor)>((members, null));
    }

    public Task<int> CountAsync(string listId, MailingListMemberStatus status, CancellationToken cancellationToken = default)
        => Task.FromResult(_members.Values.Count(member => member.ListId == listId && member.Status == status));

    public Task<int> CountNotSyncedAsync(string listId, CancellationToken cancellationToken = default)
        => Task.FromResult(_members.Values.Count(member => member.ListId == listId && member.SyncState != MailingListSyncState.Synced));

    public Task CreateAsync(MailingListMember member, CancellationToken cancellationToken = default)
    {
        _members[member.ItemId] = member;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(MailingListMember member, CancellationToken cancellationToken = default)
    {
        _members[member.ItemId] = member;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(MailingListMember member, CancellationToken cancellationToken = default)
    {
        _members.Remove(member.ItemId);

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Commits++;

        return Task.CompletedTask;
    }
}
