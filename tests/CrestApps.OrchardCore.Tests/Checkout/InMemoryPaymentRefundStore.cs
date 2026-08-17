using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// An in-memory <see cref="IPaymentRefundStore"/> for exercising the durable refund ledger without a
/// database.
/// </summary>
internal sealed class InMemoryPaymentRefundStore : IPaymentRefundStore
{
    private readonly Dictionary<string, PaymentRefund> _refunds = new(StringComparer.Ordinal);

    public ValueTask CreateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refund.ItemId))
        {
            refund.ItemId = UniqueId.GenerateId();
        }

        _refunds[refund.ItemId] = refund;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        _refunds[refund.ItemId] = refund;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_refunds.Remove(refund.ItemId));

    public ValueTask<PaymentRefund> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_refunds.GetValueOrDefault(id));

    public ValueTask<IReadOnlyCollection<PaymentRefund>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<PaymentRefund>>(_refunds.Values.Where(r => ids.Contains(r.ItemId, StringComparer.Ordinal)).ToArray());

    public ValueTask<IReadOnlyCollection<PaymentRefund>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<PaymentRefund>>(_refunds.Values.ToArray());

    public ValueTask<PageResult<PaymentRefund>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var entries = _refunds.Values.ToArray();

        return ValueTask.FromResult(new PageResult<PaymentRefund>
        {
            Count = entries.Length,
            Entries = entries,
        });
    }

    public Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey));

    public Task<IEnumerable<PaymentRefund>> GetByOriginalTransactionAsync(string originalTransactionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.Where(r => r.OriginalTransactionId == originalTransactionId));

    public Task<IEnumerable<PaymentRefund>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.Where(r => r.SessionId == sessionId));
}
