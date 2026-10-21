using CrestApps.Core.Services;
using CrestApps.OrchardCore.Memberships.Indexes;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Memberships.Services;

public interface IMembershipStore : ICatalog<Membership>
{
    Task<IReadOnlyList<Membership>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Membership>> ListByOrderAsync(string userId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active memberships that ended before <paramref name="utcNow"/>.
    /// </summary>
    Task<IReadOnlyList<Membership>> ListEndedAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<Membership> Memberships)> PageAsync(string search, MembershipStatus? status, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class MembershipStore : DocumentCatalog<Membership, MembershipIndex>, IMembershipStore
{
    public MembershipStore(ISession session)
        : base(session)
    {
        CollectionName = MembershipsConstants.CollectionName;
    }

    public async Task<IReadOnlyList<Membership>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : [.. await Session.Query<Membership, MembershipIndex>(index => index.UserId == userId, collection: CollectionName).OrderByDescending(index => index.CreatedUtc).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Membership>> ListByOrderAsync(string userId, string orderId, CancellationToken cancellationToken = default)
        => [.. (await ListByUserAsync(userId, cancellationToken)).Where(membership => membership.OrderIds.Contains(orderId))];

    public async Task<IReadOnlyList<Membership>> ListEndedAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Membership, MembershipIndex>(index => index.Status == MembershipStatus.Active && index.EndsUtc != null && index.EndsUtc < utcNow, collection: CollectionName)
            .OrderBy(index => index.EndsUtc)
            .Take(take)
            .ListAsync(cancellationToken)];

    public async Task<(int Count, IReadOnlyList<Membership> Memberships)> PageAsync(string search, MembershipStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Membership, MembershipIndex>(collection: CollectionName);

        if (status is { } wanted)
        {
            query = query.Where(index => index.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var start = search.Trim();

            query = query.Where(index => index.UserName.StartsWith(start));
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}
