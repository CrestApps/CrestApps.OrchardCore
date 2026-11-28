using OrchardCore;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Commerce.Handlers;

/// <summary>
/// Records what a paid order line bought as a pending purchase, and asks for it back after a full refund or a
/// cancellation. Both run inside the unit of work that paid or refunded the order and commit nothing themselves: the
/// grants are made by <see cref="LearningPurchaseFulfillment"/> after that unit of work committed.
/// </summary>
internal sealed class LearningOrderEventHandler : OrderLineGrantHandlerBase<LearningProductPart>
{
    private readonly IContentManager _contentManager;
    private readonly LearningPurchaseStore _purchases;
    private readonly ILeasedWorkQueue<LearningFulfillmentWork> _queue;
    private readonly IClock _clock;

    public LearningOrderEventHandler(IContentManager contentManager, LearningPurchaseStore purchases, ILeasedWorkQueue<LearningFulfillmentWork> queue, IClock clock)
        : base(contentManager)
    {
        _contentManager = contentManager;
        _purchases = purchases;
        _queue = queue;
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
        var purchase = new LearningPurchase
        {
            ItemId = IdGenerator.GenerateId(),
            OrderId = order.ItemId,
            LineId = line.LineId,
            UserId = order.UserId,
            AccountId = order.AccountId,
            ProductId = item.ContentItemId,
            State = LearningPurchaseState.Pending,
            GrantedUtc = _clock.UtcNow,
        };
        var grants = context.Part.Grants.Count > 0
            ? context.Part.Grants
            : [new LearningProductGrant { Kind = item.Has<LearningPathPart>() ? LearningProductGrantKind.LearningPath : LearningProductGrantKind.Course }];

        foreach (var grant in grants)
        {
            var targetId = string.IsNullOrEmpty(grant.TargetId) ? item.ContentItemId : grant.TargetId;

            purchase.Grants.Add(new LearningPurchaseGrant
            {
                Kind = grant.Kind,
                TargetId = targetId,
                Title = targetId == item.ContentItemId ? item.DisplayText : (await _contentManager.GetAsync(targetId, VersionOptions.Published))?.DisplayText ?? item.DisplayText,
                AccessDays = grant.AccessDays,
                Seats = grant.Kind == LearningProductGrantKind.Seats ? Math.Max(1, grant.Seats) * Math.Max(1, line.Quantity) : 0,
                EnrollsWithoutKeyOrApproval = context.Part.EnrollsWithoutKeyOrApproval,
            });
        }

        await _purchases.CreateAsync(purchase, cancellationToken);
        await EnqueueAsync(new LearningFulfillmentWork { PurchaseId = purchase.ItemId }, cancellationToken);

        return true;
    }

    protected override async Task RevokeLineAsync(Order order, OrderLine line, OrderRevokeReason reason, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.FindAsync(order.ItemId, line.LineId, cancellationToken);

        if (purchase is null || purchase.State == LearningPurchaseState.Revoked)
        {
            return;
        }

        // The work carries the request, so it never overwrites what fulfilment is recording on the purchase.
        await EnqueueAsync(new LearningFulfillmentWork
        {
            PurchaseId = purchase.ItemId,
            Revoke = true,
            Reason = reason == OrderRevokeReason.Refunded ? "The order was refunded." : "The order was canceled.",
        }, cancellationToken);
    }

    // The work joins the caller's unit of work, so it exists only when the payment or the refund committed; it is run
    // right after the request, and the background task runs whatever is still due.
    private async Task EnqueueAsync(LearningFulfillmentWork work, CancellationToken cancellationToken)
    {
        work.ItemId = IdGenerator.GenerateId();
        await _queue.EnqueueAsync(work, cancellationToken);

        if (ShellScope.Current is not null)
        {
            var workId = work.ItemId;

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<LearningPurchaseFulfillment>().RunAsync(workId));
        }
    }
}
