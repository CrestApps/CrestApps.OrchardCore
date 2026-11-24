using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

using YesSql;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Invoices numbered by the site's generator over the number sequence in the store: an issue that fails after taking its
/// number gives it to the next invoice, and invoices issued at the same time from separate units of work get distinct,
/// consecutive numbers because the series lock is held until the number is saved.
/// </summary>
public sealed class InvoiceNumberingTests
{
    [Fact]
    public async Task AnIssueThatFailsAfterTakingItsNumber_LeavesNoGap()
    {
        // Arrange: posting the transaction, which comes after the invoice took its number, fails once.
        await using var harness = await InvoicingTestHarness.CreateAsync(realNumbers: true);
        var failing = await harness.CreateDraftAsync();
        var next = await harness.CreateDraftAsync();
        harness.FailNextTransaction = true;

        // Act: each issue runs as a request, whose session is saved when it ends, even after a failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.InRequestAsync(services => services.GetRequiredService<IInvoiceCommandService>().IssueAsync(failing.ItemId, TestContext.Current.CancellationToken)));

        var issued = await harness.InRequestAsync(services => services.GetRequiredService<IInvoiceCommandService>().IssueAsync(next.ItemId, TestContext.Current.CancellationToken));

        // Assert: the number the failed issue took went to the next invoice.
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal("INV-1001", issued.Invoice.Number);
        Assert.Null((await harness.FindInvoiceAsync(failing.ItemId)).Number);
    }

    [Fact]
    public async Task InvoicesIssuedAtTheSameTime_GetDistinctConsecutiveNumbers()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(realNumbers: true);

        // Act
        var numbers = await IssueTogetherAsync(harness);

        // Assert
        Assert.Equal(["INV-1001", "INV-1002", "INV-1003"], numbers.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The same issues with a series lock that locks nothing: they don't all get distinct consecutive numbers (they collide
    /// on a number, or the sequence's concurrency check refuses all but one), which shows the test above depends on the
    /// lock and would fail without it.
    /// </summary>
    [Fact]
    public async Task WithoutTheSeriesLock_InvoicesIssuedAtTheSameTime_CollideOnTheirNumbers()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(realNumbers: true, numberLock: new NoLock());

        // Act
        var numbers = await IssueTogetherAsync(harness);

        // Assert
        Assert.NotEqual(["INV-1001", "INV-1002", "INV-1003"], numbers.Order(StringComparer.Ordinal));
    }

    // Issues three drafts at once, each in its own unit of work, with a pause between taking the number and saving it.
    private static async Task<IReadOnlyList<string>> IssueTogetherAsync(InvoicingTestHarness harness)
    {
        harness.TransactionHandlers.Add(new SlowTransactions { Delay = TimeSpan.FromMilliseconds(300) });

        var drafts = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            drafts.Add((await harness.CreateDraftAsync()).ItemId);
        }

        var results = await Task.WhenAll(drafts.Select(async id =>
        {
            try
            {
                var result = await harness.InScopeAsync(services =>
                    services.GetRequiredService<IInvoiceCommandService>().IssueAsync(id, TestContext.Current.CancellationToken));

                return result.Succeeded ? result.Invoice.Number : $"failed: {string.Join(" ", result.Errors)}";
            }
            catch (ConcurrencyException ex)
            {
                return $"failed: {ex.Message}";
            }
        }));

        return results;
    }

    /// <summary>
    /// Runs while the transaction an invoice posts is created: after the invoice took its number, before it is saved.
    /// </summary>
    private sealed class SlowTransactions : CatalogEntryHandlerBase<Transaction>
    {
        public TimeSpan Delay { get; set; }

        public override async Task CreatingAsync(CreatingContext<Transaction> context, CancellationToken cancellationToken = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// A lock that always grants and holds nothing.
    /// </summary>
    private sealed class NoLock : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new Locker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>((new Locker(), true));

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(false);

        private sealed class Locker : ILocker
        {
            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            public void Dispose()
            {
            }
        }
    }
}
