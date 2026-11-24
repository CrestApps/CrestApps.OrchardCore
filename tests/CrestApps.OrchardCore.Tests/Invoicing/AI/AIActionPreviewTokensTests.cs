using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// The shared preview tokens (moved from the Orders refund tools to AI.Core so Invoicing can use them too) work once, for
/// the caller, purpose and fingerprint they were issued for, and only until they expire: a refund token never voids an
/// invoice, and an invoice token never refunds an order.
/// </summary>
public sealed class AIActionPreviewTokensTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private readonly LocalLock _locks = new(NullLogger<LocalLock>.Instance);

    [Fact]
    public async Task TakeAsync_TakesATokenOnce()
    {
        // Arrange
        var (token, expiresUtc) = await AIActionPreviewTokens.IssueAsync(_cache, "invoicing-money-preview", "site|user-1", "invoice-void|inv-1|100", _now, TestContext.Current.CancellationToken);

        // Act
        var first = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "invoicing-money-preview", "site|user-1", token, "invoice-void|inv-1|100", _now, TestContext.Current.CancellationToken);
        var second = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "invoicing-money-preview", "site|user-1", token, "invoice-void|inv-1|100", _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.Add(AIActionPreviewTokens.Lifetime), expiresUtc);
        Assert.Equal((AIActionPreviewTokenResult.Taken, AIActionPreviewTokenResult.Unknown), (first, second));
    }

    [Fact]
    public async Task TakeAsync_RefusesAnotherPurposeCallerOrFingerprint_AndAnExpiredToken()
    {
        // Arrange
        var (token, _) = await AIActionPreviewTokens.IssueAsync(_cache, "orders-refund-preview", "site|user-1", "order-1|25|USD", _now, TestContext.Current.CancellationToken);

        // Act
        var otherPurpose = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "invoicing-money-preview", "site|user-1", token, "order-1|25|USD", _now, TestContext.Current.CancellationToken);
        var otherCaller = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "orders-refund-preview", "site|user-2", token, "order-1|25|USD", _now, TestContext.Current.CancellationToken);
        var otherAction = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "orders-refund-preview", "site|user-1", token, "order-1|30|USD", _now, TestContext.Current.CancellationToken);
        var expired = await AIActionPreviewTokens.TakeAsync(_cache, _locks, "orders-refund-preview", "site|user-1", token, "order-1|25|USD", _now.AddMinutes(11), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIActionPreviewTokenResult.Unknown, otherPurpose);
        Assert.Equal(AIActionPreviewTokenResult.Unknown, otherCaller);
        Assert.Equal(AIActionPreviewTokenResult.Mismatch, otherAction);
        Assert.Equal(AIActionPreviewTokenResult.Expired, expired);
    }
}
