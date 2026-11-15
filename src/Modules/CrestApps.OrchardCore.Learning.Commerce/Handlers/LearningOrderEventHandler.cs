using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Commerce.Handlers;

/// <summary>
/// Grants what a paid order line bought: enrollments of the buyer, a started learning path, or a licence pool for the
/// buyer's account. Each line's grants are kept in a purchase record, so a full refund or a cancellation takes exactly
/// those back.
/// </summary>
internal sealed class LearningOrderEventHandler : OrderLineGrantHandlerBase<LearningProductPart>
{
    private readonly LearningPurchaseStore _purchases;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;

    public LearningOrderEventHandler(IContentManager contentManager, LearningPurchaseStore purchases, IServiceProvider services, IClock clock)
        : base(contentManager)
    {
        _purchases = purchases;
        _services = services;
        _clock = clock;
    }

    protected override Task<bool> CanGrantAsync(Order order, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(order.UserId) || !string.IsNullOrEmpty(order.AccountId));

    protected override async Task<ISet<string>> GetGrantedLineIdsAsync(Order order, CancellationToken cancellationToken)
        => (await _purchases.ListByOrderAsync(order.ItemId, cancellationToken)).Select(purchase => purchase.LineId).ToHashSet(StringComparer.Ordinal);

    protected override async Task<bool> GrantLineAsync(OrderLineGrantContext<LearningProductPart> context, CancellationToken cancellationToken)
    {
        var order = context.Order;
        var line = context.Line;
        var item = context.ContentItem;
        var sourceId = $"{order.ItemId}:{line.LineId}";
        var purchase = new LearningPurchase { OrderId = order.ItemId, LineId = line.LineId, UserId = order.UserId, AccountId = order.AccountId, ProductId = item.ContentItemId, GrantedUtc = _clock.UtcNow };
        var grants = context.Part.Grants.Count > 0
            ? context.Part.Grants
            : [new LearningProductGrant { Kind = item.Has<LearningPathPart>() ? LearningProductGrantKind.LearningPath : LearningProductGrantKind.Course }];

        foreach (var grant in grants)
        {
            var targetId = string.IsNullOrEmpty(grant.TargetId) ? item.ContentItemId : grant.TargetId;
            var accessEnds = grant.AccessDays is > 0 ? _clock.UtcNow.AddDays(grant.AccessDays.Value) : (DateTime?)null;

            switch (grant.Kind)
            {
                case LearningProductGrantKind.Course when !string.IsNullOrEmpty(order.UserId):
                    var enrolled = await _services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
                    {
                        UserId = order.UserId,
                        CourseId = targetId,
                        Source = LearningCommerceConstants.PurchaseSource,
                        SourceId = sourceId,
                        AccessEndsUtc = accessEnds,
                        Actor = "Order",
                    }, cancellationToken);

                    if (enrolled.Enrollment is not null)
                    {
                        purchase.EnrollmentIds.Add(enrolled.Enrollment.ItemId);
                    }

                    break;

                case LearningProductGrantKind.LearningPath when !string.IsNullOrEmpty(order.UserId) && _services.GetService<ILearningPathService>() is { } paths:
                    var started = await paths.StartAsync(new LearningPathStartRequest { PathId = targetId, UserId = order.UserId, Source = LearningCommerceConstants.PurchaseSource, SourceId = sourceId, Actor = "Order" }, cancellationToken);

                    if (started.Progress is not null)
                    {
                        purchase.PathIds.Add(started.Progress.ItemId);
                    }

                    break;

                case LearningProductGrantKind.Seats:
                    var accountId = !string.IsNullOrEmpty(order.AccountId)
                        ? order.AccountId
                        : string.IsNullOrEmpty(order.UserId) ? null : (await _services.GetRequiredService<IAccountMemberService>().FindAccountAsync(order.UserId, cancellationToken))?.ContentItemId;

                    if (accountId is null)
                    {
                        break;
                    }

                    var pool = await _services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool
                    {
                        AccountId = accountId,
                        CourseId = targetId,
                        Title = targetId == item.ContentItemId ? item.DisplayText : (await _services.GetRequiredService<IContentManager>().GetAsync(targetId, VersionOptions.Published))?.DisplayText ?? item.DisplayText,
                        Seats = Math.Max(1, grant.Seats) * Math.Max(1, line.Quantity),
                        AccessDays = grant.AccessDays,
                        OrderId = order.ItemId,
                        LineId = line.LineId,
                    }, cancellationToken);

                    purchase.PoolIds.Add(pool.ItemId);
                    purchase.AccountId = accountId;

                    break;
            }
        }

        if (purchase.EnrollmentIds.Count == 0 && purchase.PathIds.Count == 0 && purchase.PoolIds.Count == 0)
        {
            return false;
        }

        await _purchases.CreateAsync(purchase, cancellationToken);

        return true;
    }

    protected override async Task RevokeLineAsync(Order order, OrderLine line, OrderRevokeReason reason, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.FindAsync(order.ItemId, line.LineId, cancellationToken);

        if (purchase is null || purchase.RevokedUtc is not null)
        {
            return;
        }

        var why = reason == OrderRevokeReason.Refunded ? "The order was refunded." : "The order was canceled.";

        await _services.GetRequiredService<IEnrollmentService>().RevokeGrantAsync(LearningCommerceConstants.PurchaseSource, $"{order.ItemId}:{line.LineId}", "Order", why, cancellationToken);

        foreach (var poolId in purchase.PoolIds)
        {
            await _services.GetRequiredService<ILicensePoolService>().RevokeAsync(poolId, why, cancellationToken);
        }

        // Taking enrollments back commits the session: read the purchase again before changing it.
        purchase = await _purchases.FindAsync(order.ItemId, line.LineId, cancellationToken);
        purchase.RevokedUtc = _clock.UtcNow;
        await _purchases.UpdateAsync(purchase, cancellationToken);
    }
}
