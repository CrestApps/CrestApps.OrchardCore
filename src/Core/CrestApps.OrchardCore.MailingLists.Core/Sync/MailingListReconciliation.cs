using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Reads, once a day, the changes of each list whose provider reports them, and applies them through
/// <see cref="IMailingListMemberService.ApplyProviderChangeAsync"/>, because providers drop or turn off webhooks that fail
/// for long. Each list is reconciled in a scope of its own, and the time it started is kept on the list as the cursor of
/// the next run.
/// </summary>
public sealed class MailingListReconciliation
{
    /// <summary>
    /// The most pages one list reads in one run.
    /// </summary>
    public const int MaxPagesPerList = 200;

    private static readonly TimeSpan _firstWindow = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListReconciliation"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory, for a scope per list.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public MailingListReconciliation(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<MailingListReconciliation> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Reconciles every list whose provider reports its changes.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many member changes were applied.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> listIds;

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IMailingListProviderResolver>();
            var lists = await scope.ServiceProvider.GetRequiredService<IMailingListManager>().GetAllAsync(cancellationToken);

            listIds = [.. lists
                .Where(list => resolver.Find(list.ProviderName) is { } provider
                    && !string.Equals(provider.Name, MailingListsConstants.LocalProviderName, StringComparison.OrdinalIgnoreCase)
                    && provider.Capabilities.HasFlag(MailingListProviderCapabilities.ReportsChanges))
                .Select(list => list.ItemId)];
        }

        var applied = 0;

        foreach (var listId in listIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            applied += await ReconcileAsync(listId, cancellationToken);
        }

        return applied;
    }

    /// <summary>
    /// Reconciles one list with its provider.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many member changes were applied.</returns>
    public async Task<int> ReconcileAsync(string listId, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<IMailingListManager>();
        var list = await manager.FindByIdAsync(listId, cancellationToken);
        var provider = list is null ? null : services.GetRequiredService<IMailingListProviderResolver>().Find(list.ProviderName);

        if (provider is null || !provider.Capabilities.HasFlag(MailingListProviderCapabilities.ReportsChanges) || !await provider.IsConfiguredAsync(cancellationToken))
        {
            return 0;
        }

        var memberService = services.GetRequiredService<IMailingListMemberService>();
        var state = list.TryGet<MailingListReconciliationState>(out var kept) && kept is not null ? kept : new MailingListReconciliationState();
        var started = _clock.UtcNow;
        var since = state.LastRunUtc ?? started - _firstWindow;
        var applied = 0;
        string error = null;

        try
        {
            string cursor = null;

            for (var page = 0; page < MaxPagesPerList; page++)
            {
                var changes = await provider.GetChangesAsync(list, since, cursor, cancellationToken) ?? MailingListProviderChangePage.Empty;

                foreach (var change in changes.Changes)
                {
                    if (await memberService.ApplyProviderChangeAsync(ForList(change, list.ItemId), cancellationToken))
                    {
                        applied++;
                    }
                }

                cursor = changes.NextCursor;

                if (string.IsNullOrEmpty(cursor))
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = exception.Message;
            _logger.LogWarning(exception, "The reconciliation of mailing list {ListId} with its provider failed.", listId.SanitizeLogValue());
        }

        // A failed run keeps the old cursor, so the next run reads the same changes again.
        list = await manager.FindByIdAsync(listId, cancellationToken) ?? list;
        list.Put(new MailingListReconciliationState
        {
            LastRunUtc = error is null ? started : state.LastRunUtc,
            LastApplied = applied,
            LastError = error,
        });

        await manager.UpdateAsync(list, cancellationToken: cancellationToken);
        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return applied;
    }

    private static MailingListProviderChange ForList(MailingListProviderChange change, string listId)
        => string.Equals(change.ListId, listId, StringComparison.Ordinal)
            ? change
            : new MailingListProviderChange
            {
                ListId = listId,
                Email = change.Email,
                NewEmail = change.NewEmail,
                Status = change.Status,
                Verified = change.Verified,
                Name = change.Name,
                ProviderMemberId = change.ProviderMemberId,
                Reason = change.Reason,
                OccurredUtc = change.OccurredUtc,
            };
}
