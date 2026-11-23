using System.Diagnostics;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Sends the member changes of the provider outbox to the lists' providers. Each change is claimed from the leased queue
/// and sent in a scope of its own; a failure the provider says can pass is tried again with a back-off, and after the
/// last attempt, or a refusal, the member is marked "not synced" with the provider's error. A change whose provider is
/// not registered (its feature is off) or not set up waits without using up an attempt, the member shows why, and the
/// change goes out once the provider is back.
/// </summary>
public sealed class MailingListSyncProcessor
{
    private const int MaxErrorLength = 1_000;

    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);
    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(1), TimeSpan.FromHours(6));

    private readonly ILeasedWorkQueue<MailingListSyncItem> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MailingListsOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListSyncProcessor"/> class.
    /// </summary>
    /// <param name="queue">The provider outbox.</param>
    /// <param name="scopeFactory">The scope factory, for a scope per change.</param>
    /// <param name="options">The mailing lists options, for the batch size and the attempts.</param>
    /// <param name="logger">The logger.</param>
    public MailingListSyncProcessor(
        ILeasedWorkQueue<MailingListSyncItem> queue,
        IServiceScopeFactory scopeFactory,
        IOptions<MailingListsOptions> options,
        ILogger<MailingListSyncProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets how many changes one batch claims.
    /// </summary>
    public int BatchSize
        => Math.Max(1, _options.SyncBatchSize);

    /// <summary>
    /// Sends one batch of the changes that are due, oldest first.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>What the batch did.</returns>
    public async Task<MailingListSyncBatchResult> RunBatchAsync(CancellationToken cancellationToken = default)
    {
        var claimed = await _queue.ClaimDueAsync(BatchSize, _lease, cancellationToken);
        var sent = 0;
        var retried = 0;
        var waiting = 0;
        var abandoned = 0;

        foreach (var item in claimed)
        {
            switch (await SettleAsync(item, cancellationToken))
            {
                case SyncOutcome.Sent:
                    sent++;
                    break;

                case SyncOutcome.Retried:
                    retried++;
                    break;

                case SyncOutcome.Waiting:
                    waiting++;
                    break;

                default:
                    abandoned++;
                    break;
            }
        }

        return new MailingListSyncBatchResult(claimed.Count, sent, retried, abandoned, waiting);
    }

    /// <summary>
    /// Sends one change when it is still due and no other worker claimed it, right after the request that queued it.
    /// </summary>
    /// <param name="itemId">The change's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the change was tried.</returns>
    public async Task RunAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        var item = await _queue.TryClaimAsync(itemId, _lease, cancellationToken);

        if (item is not null)
        {
            await SettleAsync(item, cancellationToken);
        }
    }

    private async Task<SyncOutcome> SettleAsync(MailingListSyncItem item, CancellationToken cancellationToken)
    {
        string providerName = null;
        MailingListPushResult result;

        try
        {
            // A scope of its own: what one change commits, or fails to commit, never reaches the next.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var services = scope.ServiceProvider;
            var list = await services.GetRequiredService<IMailingListManager>().FindByIdAsync(item.ListId, cancellationToken);

            if (list is null || list.IsLocal())
            {
                // The list is gone or kept on the site now: nothing is left to send.
                await _queue.CompleteAsync(item, cancellationToken: cancellationToken);

                return SyncOutcome.Sent;
            }

            var members = services.GetRequiredService<IMailingListMemberStore>();
            var member = item.Kind == MailingListPushKind.Erase ? Erased(item) : await members.FindByIdAsync(item.MemberId, cancellationToken);

            if (member is null)
            {
                await _queue.CompleteAsync(item, cancellationToken: cancellationToken);

                return SyncOutcome.Sent;
            }

            var provider = services.GetRequiredService<IMailingListProviderResolver>().Find(list.GetProviderName());

            providerName = provider?.Name ?? list.GetProviderName();

            // A provider whose feature is off, or that is not set up, can't take the change yet. The change waits without
            // using up an attempt, an erasure included, and goes out once the provider is back.
            if (provider is null)
            {
                return await WaitAsync(item, members, member, MailingListSyncWaits.ProviderMissing(providerName), cancellationToken);
            }

            if (!await provider.IsConfiguredAsync(cancellationToken))
            {
                return await WaitAsync(item, members, member, MailingListSyncWaits.ProviderNotConfigured(providerName), cancellationToken);
            }

            result = await provider.PushAsync(list, member, item.Kind, cancellationToken) ?? MailingListPushResult.Failure("The provider gave no answer.", canRetry: true);

            if (result.Succeeded)
            {
                await _queue.CompleteAsync(item, cancellationToken: cancellationToken);

                if (item.Kind != MailingListPushKind.Erase)
                {
                    await MarkAsync(members, member, MailingListSyncState.Synced, error: null, result.ProviderMemberId, cancellationToken);
                }

                return SyncOutcome.Sent;
            }

            return await FailAsync(item, providerName, result, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The mailing list change {ItemId} could not be sent to its provider and is tried again.", item.ItemId.SanitizeLogValue());

            return await FailAsync(item, providerName, MailingListPushResult.Failure(exception.Message, canRetry: true), cancellationToken);
        }
    }

    private async Task<SyncOutcome> WaitAsync(MailingListSyncItem item, IMailingListMemberStore members, MailingListMember member, string reason, CancellationToken cancellationToken)
    {
        var status = await _queue.RetryAsync(item, reason, _ => MailingListSyncWaits.Delay, countAttempt: false, cancellationToken: cancellationToken);

        // A lost claim (the lease ran out and another worker took the change) is settled by that worker.
        if (status is not null && item.Kind != MailingListPushKind.Erase)
        {
            await MarkAsync(members, member, MailingListSyncState.Pending, reason, providerMemberId: null, cancellationToken);
        }

        return SyncOutcome.Waiting;
    }

    private async Task<SyncOutcome> FailAsync(MailingListSyncItem item, string providerName, MailingListPushResult result, CancellationToken cancellationToken)
    {
        var error = ErrorText(result.Error);
        var lastAttempt = item.AttemptCount + 1 >= Math.Max(1, _options.SyncMaxAttempts);
        var retry = result.CanRetry && !lastAttempt;

        MailingListsTelemetry.SyncFailures.Add(1, new TagList
        {
            { MailingListsTelemetry.ProviderTag, string.IsNullOrEmpty(providerName) ? "unknown" : providerName },
            { "retry", retry },
        });

        if (retry)
        {
            var status = await _queue.RetryAsync(item, error, _backoff, cancellationToken: cancellationToken);

            if (status != LeasedWorkItemStatus.DeadLettered)
            {
                return SyncOutcome.Retried;
            }
        }
        else
        {
            await _queue.AbandonAsync(item, error, cancellationToken: cancellationToken);
        }

        if (item.Kind != MailingListPushKind.Erase)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var store = scope.ServiceProvider.GetRequiredService<IMailingListMemberStore>();

            if (await store.FindByIdAsync(item.MemberId, cancellationToken) is { } fresh)
            {
                await MarkAsync(store, fresh, MailingListSyncState.Failed, error, providerMemberId: null, cancellationToken);
            }
        }

        _logger.LogWarning("The mailing list change {ItemId} was given up after {Attempts} attempts: {Error}", item.ItemId.SanitizeLogValue(), item.AttemptCount + 1, error.SanitizeLogValue());

        return SyncOutcome.Abandoned;
    }

    private static async Task MarkAsync(IMailingListMemberStore members, MailingListMember member, MailingListSyncState state, string error, string providerMemberId, CancellationToken cancellationToken)
    {
        // A change that waits again for the same reason leaves the member as it is.
        if (member.SyncState == state
            && string.Equals(member.SyncError, error, StringComparison.Ordinal)
            && (string.IsNullOrEmpty(providerMemberId) || string.Equals(member.ProviderMemberId, providerMemberId, StringComparison.Ordinal)))
        {
            return;
        }

        member.SyncState = state;
        member.SyncError = error;

        if (!string.IsNullOrEmpty(providerMemberId))
        {
            member.ProviderMemberId = providerMemberId;
        }

        await members.UpdateAsync(member, cancellationToken);
        await members.SaveChangesAsync(cancellationToken);
    }

    private static MailingListMember Erased(MailingListSyncItem item)
        => string.IsNullOrEmpty(item.Email)
            ? null
            : new MailingListMember
            {
                ItemId = item.MemberId,
                ListId = item.ListId,
                NormalizedEmail = item.Email,
                Contact = new ContactInfo { Email = item.Email },
                ProviderMemberId = item.ProviderMemberId,
                Status = MailingListMemberStatus.Unsubscribed,
            };

    private static string ErrorText(string error)
    {
        var text = string.IsNullOrWhiteSpace(error) ? "The provider did not take the change." : error.Trim();

        return text.Length > MaxErrorLength ? text[..MaxErrorLength] : text;
    }

    private enum SyncOutcome
    {
        Sent,
        Retried,
        Waiting,
        Abandoned,
    }
}
