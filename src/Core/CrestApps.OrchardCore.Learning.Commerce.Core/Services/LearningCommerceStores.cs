using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Services;

/// <summary>
/// The seat ledger: the account is the owning account, the bucket the pool.
/// </summary>
public sealed class SeatLedger
{
}

public sealed class LearningPurchaseStore : DocumentCatalog<LearningPurchase, LearningPurchaseIndex>
{
    public LearningPurchaseStore(ISession session)
        : base(session)
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<LearningPurchase>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => (await Session.Query<LearningPurchase, LearningPurchaseIndex>(index => index.OrderId == orderId, collection: CollectionName).ListAsync(cancellationToken)).ToArray();

    public Task<LearningPurchase> FindAsync(string orderId, string lineId, CancellationToken cancellationToken = default)
        => Session.Query<LearningPurchase, LearningPurchaseIndex>(index => index.OrderId == orderId && index.LineId == lineId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

public sealed class LicensePoolStore : DocumentCatalog<LicensePool, LicensePoolIndex>
{
    public LicensePoolStore(ISession session)
        : base(session)
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<LicensePool>> ListByAccountAsync(string accountId, CancellationToken cancellationToken = default)
        => (await Session.Query<LicensePool, LicensePoolIndex>(index => index.AccountId == accountId, collection: CollectionName).ListAsync(cancellationToken))
            .OrderByDescending(pool => pool.CreatedUtc)
            .ToArray();

    public Task<LicensePool> FindByLineAsync(string orderId, string lineId, CancellationToken cancellationToken = default)
        => Session.Query<LicensePool, LicensePoolIndex>(index => index.OrderId == orderId && index.LineId == lineId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

public sealed class LicenseSeatStore : DocumentCatalog<LicenseSeat, LicenseSeatIndex>
{
    public LicenseSeatStore(ISession session)
        : base(session)
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<LicenseSeat>> ListByPoolAsync(string poolId, CancellationToken cancellationToken = default)
        => (await Session.Query<LicenseSeat, LicenseSeatIndex>(index => index.PoolId == poolId, collection: CollectionName).ListAsync(cancellationToken))
            .OrderBy(seat => seat.GivenUtc)
            .ToArray();

    public Task<LicenseSeat> FindByInvitationAsync(string invitationId, CancellationToken cancellationToken = default)
        => Session.Query<LicenseSeat, LicenseSeatIndex>(index => index.InvitationId == invitationId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LicenseSeat>> ListForPeopleAsync(IReadOnlyCollection<string> userIds, IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken = default)
    {
        var users = userIds.ToArray();
        var emails = normalizedEmails.ToArray();

        if (users.Length == 0 && emails.Length == 0)
        {
            return [];
        }

        return (await Session.Query<LicenseSeat, LicenseSeatIndex>(index => index.UserId.IsIn(users) || index.NormalizedEmail.IsIn(emails), collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
    }
}
