using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Opens the renewals whose window has come, and catches up on completions whose event was lost: the people who met a
/// path's requirements since the last look, and everyone still working on a cycle, are checked again. Issuing is
/// idempotent, so checking someone already certified changes nothing.
/// </summary>
public sealed class CertificationUpkeep
{
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _overlap = TimeSpan.FromHours(1);

    private const int PageSize = 500;

    private static readonly CertificationPathState[] _workingStates = [CertificationPathState.InProgress, CertificationPathState.RenewalOpen, CertificationPathState.Lapsed];

    private readonly ICertificationPathService _paths;
    private readonly CertificationPathProgressStore _progress;
    private readonly IEnumerable<ICertificationRequirement> _requirements;
    private readonly ILeasedWorkQueue<CertificationRenewalWork> _renewals;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationUpkeep"/> class.
    /// </summary>
    public CertificationUpkeep(
        ICertificationPathService paths,
        CertificationPathProgressStore progress,
        IEnumerable<ICertificationRequirement> requirements,
        ILeasedWorkQueue<CertificationRenewalWork> renewals,
        ISession session,
        IClock clock,
        ILocalClock localClock,
        ILogger<CertificationUpkeep> logger)
    {
        _paths = paths;
        _progress = progress;
        _requirements = requirements;
        _renewals = renewals;
        _session = session;
        _clock = clock;
        _localClock = localClock;
        _logger = logger;
    }

    /// <summary>
    /// Runs the upkeep once.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many certificates were issued or renewed.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var work in await _renewals.ClaimDueAsync(50, _lease, cancellationToken))
        {
            try
            {
                await _paths.OpenRenewalAsync(work.ProgressId, cancellationToken);
                await _renewals.CompleteAsync(work, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await _renewals.RetryAsync(work, exception.Message, LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(5), TimeSpan.FromHours(6)), cancellationToken: cancellationToken);
            }
        }

        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var enabledPaths = (await _paths.ListAsync(cancellationToken)).Where(path => path.Enabled).ToArray();
        var enabledIds = enabledPaths.Select(path => path.ItemId).ToArray();

        // Every certified person whose renewal is due, a page at a time; a disabled path opens no renewal and is left
        // out, so it never holds a place that others need.
        for (var afterId = 0L; ;)
        {
            var due = await _progress.PageRenewalDueAsync(enabledIds, CertificationDates.ToKey(today).Value, afterId, PageSize, cancellationToken);

            foreach (var (progressId, _) in due)
            {
                await RunSafelyAsync(() => _paths.OpenRenewalAsync(progressId, cancellationToken), progressId);
            }

            if (due.Count < PageSize)
            {
                break;
            }

            afterId = due[^1].RowId;
        }

        var issued = 0;
        var checkedUtc = (await LoadStateAsync(cancellationToken)).LastCheckedUtc;
        var now = _clock.UtcNow;
        var done = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var path in enabledPaths)
        {
            var since = (checkedUtc.TryGetValue(path.ItemId, out var last) ? last : path.CreatedUtc) - _overlap;
            var users = new HashSet<string>(StringComparer.Ordinal);

            // Each requirement lists everyone who met it since the last look, however many: a bulk import of
            // completions is read to the end before the time moves on.
            foreach (var requirement in path.InitialRequirements.Concat(path.RenewalRequirements))
            {
                var kind = _requirements.FirstOrDefault(candidate => candidate.Kind == requirement.Kind);

                if (kind is not null)
                {
                    users.UnionWith(await kind.ListUsersSinceAsync(requirement, since, cancellationToken));
                }
            }

            // Everyone still working on a cycle, a page at a time in a stable order, so nobody is left out for good.
            for (var afterId = 0L; ;)
            {
                var page = await _progress.PageUserIdsByStatesAsync(path.ItemId, _workingStates, afterId, PageSize, cancellationToken);

                users.UnionWith(page.Select(row => row.UserId));

                if (page.Count < PageSize)
                {
                    break;
                }

                afterId = page[^1].RowId;
            }

            foreach (var userId in users)
            {
                var result = await RunSafelyAsync(() => _paths.EvaluateAsync(path.ItemId, userId, cancellationToken), userId);

                if (result?.IssuedRecordId is not null)
                {
                    issued++;
                }
            }

            done[path.ItemId] = now;
        }

        // Every check above commits, and a commit makes the session forget what it read: read the state again.
        var state = await LoadStateAsync(cancellationToken);

        foreach (var (pathId, time) in done)
        {
            state.LastCheckedUtc[pathId] = time;
        }

        await _session.SaveAsync(state, collection: CertificationsConstants.CollectionName, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return issued;
    }

    private async Task<CertificationUpkeepState> LoadStateAsync(CancellationToken cancellationToken)
        => await _session.Query<CertificationUpkeepState>(collection: CertificationsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new CertificationUpkeepState();

    private async Task<CertificationPathResult> RunSafelyAsync(Func<Task<CertificationPathResult>> work, string id)
    {
        try
        {
            return await work();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The certification upkeep failed for '{Id}'.", id);

            return null;
        }
    }
}
