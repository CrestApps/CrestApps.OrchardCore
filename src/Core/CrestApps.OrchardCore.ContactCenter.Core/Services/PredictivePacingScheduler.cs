using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPredictivePacingScheduler"/>.
/// </summary>
/// <remarks>
/// A request arms one deadline per queue (<c>predictive-pacing:{queueId}</c>) a debounce after it, and further requests
/// while it is armed are merged into it rather than pushing it back, so a busy queue is still paced. The run services any
/// answered call still waiting for an agent first, then runs the queue's pacing cycle through the dialer, and asks to run
/// again after the pacing interval while the cycle keeps placing calls. A queue whose waiting work is not dialed by an
/// over-dialing profile is remembered as such for a minute, so the events of other campaigns cost a lookup at most once a
/// minute.
/// </remarks>
public sealed class PredictivePacingScheduler : IPredictivePacingScheduler
{
    private static readonly TimeSpan _notOverDialedMemory = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _notOverDialedUntil = new(StringComparer.Ordinal);
    private readonly IContactCenterDeadlineScheduler _deadlineScheduler;
    private readonly IClock _clock;
    private readonly ContactCenterPredictiveDialingOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingScheduler"/> class.
    /// </summary>
    /// <param name="deadlineScheduler">The in-process deadline scheduler the runs are held on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The predictive dialing options.</param>
    /// <param name="logger">The logger.</param>
    public PredictivePacingScheduler(
        IContactCenterDeadlineScheduler deadlineScheduler,
        IClock clock,
        IOptions<ContactCenterPredictiveDialingOptions> options,
        ILogger<PredictivePacingScheduler> logger)
    {
        _deadlineScheduler = deadlineScheduler;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The key of the deadline that paces the queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    public static string GetDeadlineKey(string queueId)
        => $"predictive-pacing:{queueId}";

    /// <inheritdoc/>
    public void Request(string queueId)
    {
        if (!ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return;
        }

        var now = _clock.UtcNow;

        if (_notOverDialedUntil.TryGetValue(queueId, out var until))
        {
            if (until > now)
            {
                return;
            }

            _notOverDialedUntil.TryRemove(queueId, out _);
        }

        // Merged into the run already armed: pushing it back on every event would let a busy queue starve.
        if (!_pending.TryAdd(queueId, 0))
        {
            return;
        }

        _deadlineScheduler.Schedule(GetDeadlineKey(queueId), now.Add(_options.PacingDebounce), (services, cancellationToken) => RunAsync(services, queueId, cancellationToken));
    }

    private async Task<DateTime?> RunAsync(IServiceProvider services, string queueId, CancellationToken cancellationToken)
    {
        // A request arriving from here on arms the next run.
        _pending.TryRemove(queueId, out _);

        var queueItemStore = services.GetRequiredService<IQueueItemStore>();
        var profileReader = services.GetRequiredService<IDialerProfileReader>();

        // Answered calls already waiting for an agent come before any new call. They are connected in a scope of their
        // own, so a claim that loses a race cannot spend the session the pacing cycle commits.
        if (services.GetService<IPredictiveAgentConnector>() is not null)
        {
            await services.GetRequiredService<IContactCenterScopeExecutor>().ExecuteAsync<IPredictiveAgentConnector>(connector =>
                connector.ServiceWaitingAsync(queueId, cancellationToken));
        }

        var head = await queueItemStore.FindNextWaitingAsync(queueId, cancellationToken);

        if (head is null || string.IsNullOrEmpty(head.DialerProfileId))
        {
            return null;
        }

        var profile = await profileReader.FindByIdAsync(head.DialerProfileId, cancellationToken);

        if (profile is null ||
            !profile.Enabled ||
            profile.Mode != DialerMode.Predictive ||
            profile.PredictivePacingModel != PredictivePacingModel.OverDial)
        {
            _notOverDialedUntil[queueId] = _clock.UtcNow.Add(_notOverDialedMemory);

            return null;
        }

        var started = await services.GetRequiredService<IDialerService>().RunCycleAsync(profile, queueId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Paced over-dialing queue '{QueueId}' on request: {Started} call(s) placed.", queueId.SanitizeLogValue(), started);
        }

        // While calls are being placed the queue is paced again soon; once nothing is placed it waits for the next event
        // (an agent freeing up, a call answered or ending) or the minute dialer pacing task.
        return started > 0 ? _clock.UtcNow.Add(_options.PacingInterval) : null;
    }
}
