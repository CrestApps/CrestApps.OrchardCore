using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Runs the work on learning purchases outside the unit of work that paid or refunded the order: right after the
/// request for the work it queued, and on a schedule for whatever is still due. Each piece of work is claimed from a
/// leased queue and run in a scope of its own; a busy enrollment or a failure is retried with a back-off, and a grant
/// that can never be made is given up on once it is recorded.
/// </summary>
internal sealed class LearningPurchaseFulfillment
{
    private const int BatchSize = 25;
    private const int MaxBatches = 20;

    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);
    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

    private readonly ILeasedWorkQueue<LearningFulfillmentWork> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public LearningPurchaseFulfillment(ILeasedWorkQueue<LearningFulfillmentWork> queue, IServiceScopeFactory scopeFactory, ILogger<LearningPurchaseFulfillment> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs every piece of work that is due, oldest first, a batch at a time.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many pieces of work were settled for good.</returns>
    public async Task<int> RunDueAsync(CancellationToken cancellationToken = default)
    {
        var settled = 0;

        for (var batch = 0; batch < MaxBatches && !cancellationToken.IsCancellationRequested; batch++)
        {
            var claimed = await _queue.ClaimDueAsync(BatchSize, _lease, cancellationToken);

            foreach (var work in claimed)
            {
                if (await SettleAsync(work, cancellationToken))
                {
                    settled++;
                }
            }

            // A retried item is due later, so a short batch means nothing else is due now.
            if (claimed.Count < BatchSize)
            {
                break;
            }
        }

        return settled;
    }

    /// <summary>
    /// Runs one piece of work when it is still due and no other worker claimed it.
    /// </summary>
    /// <param name="workId">The work item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the work ran.</returns>
    public async Task RunAsync(string workId, CancellationToken cancellationToken = default)
    {
        var work = await _queue.TryClaimAsync(workId, _lease, cancellationToken);

        if (work is not null)
        {
            await SettleAsync(work, cancellationToken);
        }
    }

    private async Task<bool> SettleAsync(LearningFulfillmentWork work, CancellationToken cancellationToken)
    {
        LearningPurchaseOutcome outcome;

        try
        {
            // A scope of its own: what one purchase commits, or fails to commit, never reaches the next.
            await using var scope = _scopeFactory.CreateAsyncScope();

            outcome = await scope.ServiceProvider.GetRequiredService<LearningPurchaseProcessor>().ProcessAsync(work, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Learning purchase {PurchaseId} could not be processed and is retried.", work.PurchaseId);
            outcome = LearningPurchaseOutcome.Retry(exception.Message);
        }

        switch (outcome.Kind)
        {
            case LearningPurchaseOutcomeKind.Done:
                await _queue.CompleteAsync(work, cancellationToken: cancellationToken);

                return true;

            case LearningPurchaseOutcomeKind.GiveUp:
                await _queue.AbandonAsync(work, outcome.Message, cancellationToken: cancellationToken);

                return true;

            default:
                await _queue.RetryAsync(work, outcome.Message, _backoff, cancellationToken: cancellationToken);

                return false;
        }
    }
}

/// <summary>
/// What became of one piece of work on a purchase.
/// </summary>
internal enum LearningPurchaseOutcomeKind
{
    Done,
    Retry,
    GiveUp,
}

/// <summary>
/// What became of one piece of work on a purchase, and why.
/// </summary>
internal readonly record struct LearningPurchaseOutcome(LearningPurchaseOutcomeKind Kind, string Message)
{
    public static LearningPurchaseOutcome Done => new(LearningPurchaseOutcomeKind.Done, null);

    public static LearningPurchaseOutcome Retry(string message) => new(LearningPurchaseOutcomeKind.Retry, message);

    public static LearningPurchaseOutcome GiveUp(string message) => new(LearningPurchaseOutcomeKind.GiveUp, message);
}
