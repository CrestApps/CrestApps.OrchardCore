using CrestApps.OrchardCore.Banking.Disbursements.Services;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// What a vendor payout is sent and committed with: the Disbursements services, when that feature is on, the lock that
/// runs one payout of a vendor at a time, and the session each step is committed with.
/// </summary>
public sealed class MarketplacePayoutServices
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketplacePayoutServices"/> class.
    /// </summary>
    /// <param name="disbursements">The disbursement service, none while Disbursements is off.</param>
    /// <param name="disbursementManagers">The disbursement manager, none while Disbursements is off.</param>
    /// <param name="batchManagers">The disbursement batch manager, none while Disbursements is off.</param>
    /// <param name="distributedLock">The lock that runs one payout of a vendor at a time.</param>
    /// <param name="session">The session each payout step is committed with.</param>
    public MarketplacePayoutServices(
        IEnumerable<IDisbursementService> disbursements,
        IEnumerable<IDisbursementManager> disbursementManagers,
        IEnumerable<IDisbursementBatchManager> batchManagers,
        IDistributedLock distributedLock,
        global::YesSql.ISession session)
    {
        ArgumentNullException.ThrowIfNull(disbursements);
        ArgumentNullException.ThrowIfNull(disbursementManagers);
        ArgumentNullException.ThrowIfNull(batchManagers);
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(session);

        Disbursements = disbursements.FirstOrDefault();
        DisbursementManager = disbursementManagers.FirstOrDefault();
        BatchManager = batchManagers.FirstOrDefault();
        DistributedLock = distributedLock;
        Session = session;
    }

    /// <summary>
    /// Gets the disbursement service, or <see langword="null"/> while Disbursements is off.
    /// </summary>
    public IDisbursementService Disbursements { get; }

    /// <summary>
    /// Gets the disbursement manager, or <see langword="null"/> while Disbursements is off.
    /// </summary>
    public IDisbursementManager DisbursementManager { get; }

    /// <summary>
    /// Gets the disbursement batch manager, or <see langword="null"/> while Disbursements is off.
    /// </summary>
    public IDisbursementBatchManager BatchManager { get; }

    /// <summary>
    /// Gets the lock that runs one payout of a vendor at a time.
    /// </summary>
    public IDistributedLock DistributedLock { get; }

    /// <summary>
    /// Gets the session each payout step is committed with.
    /// </summary>
    public global::YesSql.ISession Session { get; }

    /// <summary>
    /// Gets a value indicating whether payouts can be sent: Disbursements is on.
    /// </summary>
    public bool CanDisburse
        => Disbursements is not null && DisbursementManager is not null && BatchManager is not null;
}
