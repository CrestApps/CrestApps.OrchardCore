using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Brings one learning purchase to where its order says it should be: grants what a paid line bought, or takes it back
/// after a full refund or a cancellation. It works under a lock of its own per purchase and reads the purchase fresh;
/// each grant is recorded as soon as it is made, so a retry never grants twice.
/// </summary>
internal sealed class LearningPurchaseProcessor
{
    private const string Actor = "Order";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(5);

    private readonly LearningPurchaseStore _purchases;
    private readonly LicensePoolStore _poolStore;
    private readonly IEnrollmentService _enrollments;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILicensePoolService _pools;
    private readonly IAccountMemberService _members;
    private readonly IDistributedLock _locks;
    private readonly ISession _session;
    private readonly IServiceProvider _services;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public LearningPurchaseProcessor(
        LearningPurchaseStore purchases,
        LicensePoolStore poolStore,
        IEnrollmentService enrollments,
        IEnrollmentStore enrollmentStore,
        ILicensePoolService pools,
        IAccountMemberService members,
        IDistributedLock locks,
        ISession session,
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<LearningPurchaseProcessor> logger,
        IStringLocalizer<LearningPurchaseProcessor> stringLocalizer)
    {
        _purchases = purchases;
        _poolStore = poolStore;
        _enrollments = enrollments;
        _enrollmentStore = enrollmentStore;
        _pools = pools;
        _members = members;
        _locks = locks;
        _session = session;
        _services = services;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Runs one piece of work on a purchase.
    /// </summary>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the work is done, is to be retried, or can never succeed.</returns>
    public async Task<LearningPurchaseOutcome> ProcessAsync(LearningFulfillmentWork work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var (locker, locked) = await _locks.TryAcquireLockAsync(LockKey(work.PurchaseId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return LearningPurchaseOutcome.Retry("The purchase is being changed.");
        }

        await using (locker)
        {
            var purchase = await _purchases.FindFreshByIdAsync(work.PurchaseId, cancellationToken);

            // Gone (its buyer's data was erased with it) or already taken back: nothing is left to do.
            if (purchase is null || purchase.State == LearningPurchaseState.Revoked)
            {
                return LearningPurchaseOutcome.Done;
            }

            if (work.Revoke && !purchase.RevokeRequested)
            {
                purchase = await SaveAsync(purchase.ItemId, fresh =>
                {
                    fresh.RevokeRequested = true;
                    fresh.RevokeReason = work.Reason;
                }, cancellationToken);
            }

            if (purchase.RevokeRequested)
            {
                return await RevokeAsync(purchase, cancellationToken);
            }

            return purchase.State == LearningPurchaseState.Pending
                ? await FulfillAsync(purchase, cancellationToken)
                : LearningPurchaseOutcome.Done;
        }
    }

    internal static string LockKey(string purchaseId)
        => "Learning:Purchase:" + purchaseId;

    internal static string SourceId(LearningPurchase purchase)
        => $"{purchase.OrderId}:{purchase.LineId}";

    private async Task<LearningPurchaseOutcome> FulfillAsync(LearningPurchase purchase, CancellationToken cancellationToken)
    {
        var busy = false;

        for (var index = 0; index < purchase.Grants.Count; index++)
        {
            var grant = purchase.Grants[index];

            if (grant.Done || grant.Error is not null)
            {
                continue;
            }

            var result = await GrantAsync(purchase, grant, cancellationToken);

            if (result.Busy)
            {
                busy = true;

                continue;
            }

            // Granting commits the session, which detaches what was read before: record the result on a fresh read.
            var position = index;

            purchase = await SaveAsync(purchase.ItemId, fresh =>
            {
                var recorded = fresh.Grants[position];

                recorded.Done = result.Error is null;
                recorded.Error = result.Error;
                AddOnce(fresh.EnrollmentIds, result.EnrollmentId);
                AddOnce(fresh.PathIds, result.PathProgressId);
                AddOnce(fresh.PoolIds, result.PoolId);

                if (result.AccountId is not null)
                {
                    fresh.AccountId = result.AccountId;
                }
            }, cancellationToken);
        }

        if (busy)
        {
            return LearningPurchaseOutcome.Retry("An enrollment of the buyer was being changed.");
        }

        var errors = purchase.Grants.Where(grant => grant.Error is not null).Select(grant => grant.Error).ToArray();

        if (errors.Length > 0)
        {
            purchase = await SaveAsync(purchase.ItemId, fresh =>
            {
                fresh.State = LearningPurchaseState.Failed;
                fresh.Error = string.Join(" ", errors);
            }, cancellationToken);

            _logger.LogWarning("Learning purchase {PurchaseId} of order {OrderId} could not grant everything it bought; the reason is on the purchase and on the order.", purchase.ItemId, purchase.OrderId);
            await AddOrderNoteAsync(purchase, purchase.Error, cancellationToken);

            return LearningPurchaseOutcome.GiveUp(purchase.Error);
        }

        await SaveAsync(purchase.ItemId, fresh =>
        {
            fresh.State = LearningPurchaseState.Fulfilled;
            fresh.FulfilledUtc = _clock.UtcNow;
            fresh.Error = null;
        }, cancellationToken);

        return LearningPurchaseOutcome.Done;
    }

    private async Task<GrantResult> GrantAsync(LearningPurchase purchase, LearningPurchaseGrant grant, CancellationToken cancellationToken)
    {
        switch (grant.Kind)
        {
            case LearningProductGrantKind.Course:
                if (string.IsNullOrEmpty(purchase.UserId))
                {
                    return GrantResult.Failed(S["The order has no buyer to enroll in {0}.", grant.Title]);
                }

                // The buyer was allowed to pay, so the course is theirs even when a prerequisite is not met yet.
                var enrolled = await _enrollments.EnrollAsync(new EnrollmentRequest
                {
                    UserId = purchase.UserId,
                    CourseId = grant.TargetId,
                    Source = LearningCommerceConstants.PurchaseSource,
                    SourceId = SourceId(purchase),
                    AccessEndsUtc = grant.AccessDays is > 0 ? _clock.UtcNow.AddDays(grant.AccessDays.Value) : null,
                    IgnorePrerequisites = true,
                    Actor = Actor,
                }, cancellationToken);

                if (enrolled.Enrollment is not null)
                {
                    return new GrantResult { EnrollmentId = enrolled.Enrollment.ItemId };
                }

                return enrolled.ErrorCode == EnrollmentErrors.Busy
                    ? GrantResult.WasBusy
                    : GrantResult.Failed(enrolled.ErrorCode == EnrollmentErrors.CourseNotFound
                        ? S["{0} is not published, so the buyer was not enrolled.", grant.Title]
                        : S["The buyer could not be enrolled in {0} ({1}).", grant.Title, enrolled.ErrorCode]);

            case LearningProductGrantKind.LearningPath:
                if (string.IsNullOrEmpty(purchase.UserId))
                {
                    return GrantResult.Failed(S["The order has no buyer to start {0}.", grant.Title]);
                }

                if (_services.GetService<ILearningPathService>() is not { } paths)
                {
                    return GrantResult.Failed(S["Learning paths are not enabled, so {0} was not started.", grant.Title]);
                }

                var started = await paths.StartAsync(new LearningPathStartRequest
                {
                    PathId = grant.TargetId,
                    UserId = purchase.UserId,
                    Source = LearningCommerceConstants.PurchaseSource,
                    SourceId = SourceId(purchase),
                    Actor = Actor,
                }, cancellationToken);

                return started.Progress is not null
                    ? new GrantResult { PathProgressId = started.Progress.ItemId }
                    : GrantResult.Failed(S["The buyer could not start {0} ({1}).", grant.Title, started.Error]);

            case LearningProductGrantKind.Seats:
                var accountId = !string.IsNullOrEmpty(purchase.AccountId)
                    ? purchase.AccountId
                    : string.IsNullOrEmpty(purchase.UserId) ? null : (await _members.FindAccountAsync(purchase.UserId, cancellationToken))?.ContentItemId;

                if (accountId is null)
                {
                    return GrantResult.Failed(S["The buyer belongs to no business account, so the {0} seats in {1} were not added.", grant.Seats, grant.Title]);
                }

                // Creating the pool is idempotent per order line.
                var pool = await _pools.CreateAsync(new LicensePool
                {
                    AccountId = accountId,
                    CourseId = grant.TargetId,
                    Title = grant.Title,
                    Seats = Math.Max(1, grant.Seats),
                    AccessDays = grant.AccessDays,
                    OrderId = purchase.OrderId,
                    LineId = purchase.LineId,
                }, cancellationToken);

                return new GrantResult { PoolId = pool.ItemId, AccountId = accountId };

            default:
                return GrantResult.Failed(S["{0} grants nothing this version knows.", grant.Title]);
        }
    }

    private async Task<LearningPurchaseOutcome> RevokeAsync(LearningPurchase purchase, CancellationToken cancellationToken)
    {
        var reason = purchase.RevokeReason ?? S["The order was refunded."].Value;
        var sourceId = SourceId(purchase);

        // A closed enrollment, such as a completed one, keeps its grant and its certificate.
        await _enrollments.RevokeGrantAsync(LearningCommerceConstants.PurchaseSource, sourceId, Actor, reason, cancellationToken);

        var poolIds = new HashSet<string>(purchase.PoolIds, StringComparer.Ordinal);

        if (await _poolStore.FindByLineAsync(purchase.OrderId, purchase.LineId, cancellationToken) is { } linePool)
        {
            poolIds.Add(linePool.ItemId);
        }

        foreach (var poolId in poolIds)
        {
            await _pools.RevokeAsync(poolId, reason, cancellationToken);
        }

        // An enrollment that was busy still carries the grant: the purchase is taken back only when none does.
        var remaining = (await _enrollmentStore.ListByGrantAsync(LearningCommerceConstants.PurchaseSource, sourceId, cancellationToken))
            .Where(enrollment => (enrollment.IsOpen || enrollment.Status == EnrollmentStatus.Expired) &&
                enrollment.Grants.Any(grant => grant.Source == LearningCommerceConstants.PurchaseSource && grant.SourceId == sourceId))
            .ToArray();

        if (remaining.Length > 0)
        {
            return LearningPurchaseOutcome.Retry("An enrollment of the buyer was being changed, so its access is taken back on the next try.");
        }

        await SaveAsync(purchase.ItemId, fresh =>
        {
            fresh.State = LearningPurchaseState.Revoked;
            fresh.RevokedUtc = _clock.UtcNow;
        }, cancellationToken);

        return LearningPurchaseOutcome.Done;
    }

    private async Task<LearningPurchase> SaveAsync(string purchaseId, Action<LearningPurchase> change, CancellationToken cancellationToken)
    {
        var fresh = await _purchases.FindFreshByIdAsync(purchaseId, cancellationToken);

        change(fresh);
        await _purchases.UpdateAsync(fresh, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return fresh;
    }

    // The order has concurrency checks and others change it too, so the note goes in a scope and a commit of its own,
    // and a note that can not be added never undoes what was recorded on the purchase.
    private async Task AddOrderNoteAsync(LearningPurchase purchase, string text, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var store = scope.ServiceProvider.GetService<IOrderStore>();
            var orders = scope.ServiceProvider.GetService<IOrderService>();

            if (store is null || orders is null || await store.FindByIdAsync(purchase.OrderId, cancellationToken) is not { } order)
            {
                return;
            }

            await orders.AddNoteAsync(order, text, OrderNoteKind.Event, authorName: null, cancellationToken);
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The note about learning purchase {PurchaseId} could not be added to order {OrderId}.", purchase.ItemId, purchase.OrderId);
        }
    }

    private static void AddOnce(List<string> ids, string id)
    {
        if (!string.IsNullOrEmpty(id) && !ids.Contains(id, StringComparer.Ordinal))
        {
            ids.Add(id);
        }
    }

    private sealed class GrantResult
    {
        public static GrantResult WasBusy { get; } = new() { Busy = true };

        public bool Busy { get; init; }

        public string Error { get; init; }

        public string EnrollmentId { get; init; }

        public string PathProgressId { get; init; }

        public string PoolId { get; init; }

        public string AccountId { get; init; }

        public static GrantResult Failed(LocalizedString error) => new() { Error = error.Value };
    }
}
