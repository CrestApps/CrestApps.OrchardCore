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

    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.Values;

    public Task CreateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        _refunds[refund.Id] = refund;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        _refunds[refund.Id] = refund;

        return Task.CompletedTask;
    }

    public Task<PaymentRefund> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.GetValueOrDefault(id));

    public Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey));

    public Task<IEnumerable<PaymentRefund>> GetByOriginalTransactionAsync(string originalTransactionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.Where(r => r.OriginalTransactionId == originalTransactionId));

    public Task<IEnumerable<PaymentRefund>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_refunds.Values.Where(r => r.SessionId == sessionId));
}
