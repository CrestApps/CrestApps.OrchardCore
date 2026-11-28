using OrchardCore;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Queues and runs the work on class registrations that did not finish when their appointment changed. An observer
/// that is refused (a busy enrollment, an enrollment that is suspended for now) or fails queues the work; the
/// background task runs it again with a back-off, each time in a scope of its own, on the registration as it is then,
/// until it is done or its attempts run out.
/// </summary>
internal sealed class ClassFollowUps
{
    private const int BatchSize = 25;
    private const int MaxBatches = 20;

    private static readonly TimeSpan _firstRetry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);
    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

    private readonly ILeasedWorkQueue<ClassFollowUpWork> _queue;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public ClassFollowUps(ILeasedWorkQueue<ClassFollowUpWork> queue, ISession session, IClock clock, IServiceScopeFactory scopeFactory, ILogger<ClassFollowUps> logger)
    {
        _queue = queue;
        _session = session;
        _clock = clock;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Queues work on a registration, due in a minute, and commits it.
    /// </summary>
    public async Task EnqueueAsync(string kind, string appointmentId, string actor, string error, CancellationToken cancellationToken = default)
    {
        await _queue.EnqueueAsync(new ClassFollowUpWork
        {
            ItemId = IdGenerator.GenerateId(),
            AppointmentId = appointmentId,
            Kind = kind,
            Actor = actor,
            LastError = error,
            NextAttemptUtc = _clock.UtcNow.Add(_firstRetry),
        }, cancellationToken);

        await _session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs every piece of work that is due, oldest first, a batch at a time.
    /// </summary>
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

            if (claimed.Count < BatchSize)
            {
                break;
            }
        }

        return settled;
    }

    private async Task<bool> SettleAsync(ClassFollowUpWork work, CancellationToken cancellationToken)
    {
        ClassFollowUpOutcome outcome;

        try
        {
            // A scope of its own: what one registration commits, or fails to commit, never reaches the next.
            await using var scope = _scopeFactory.CreateAsyncScope();

            outcome = await RunAsync(scope.ServiceProvider, work, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The {Kind} work of class registration {AppointmentId} failed and is retried.", work.Kind, work.AppointmentId);
            outcome = ClassFollowUpOutcome.Retry(exception.Message);
        }

        switch (outcome.Kind)
        {
            case ClassFollowUpOutcomeKind.Done:
                await _queue.CompleteAsync(work, cancellationToken: cancellationToken);

                return true;

            case ClassFollowUpOutcomeKind.GiveUp:
                await _queue.AbandonAsync(work, outcome.Message, cancellationToken: cancellationToken);

                return true;

            default:
                await _queue.RetryAsync(work, outcome.Message, _backoff, cancellationToken: cancellationToken);

                return false;
        }
    }

    private static async Task<ClassFollowUpOutcome> RunAsync(IServiceProvider services, ClassFollowUpWork work, CancellationToken cancellationToken)
    {
        var appointment = string.IsNullOrEmpty(work.AppointmentId)
            ? null
            : await services.GetRequiredService<IAppointmentService>().FindByIdAsync(work.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return ClassFollowUpOutcome.GiveUp("The registration no longer exists.");
        }

        return work.Kind switch
        {
            ClassFollowUpKinds.Attendance => await services.GetRequiredService<ClassAttendanceRecorder>().ApplyAsync(appointment, work.Actor, cancellationToken),
            ClassFollowUpKinds.ShiftSegments => services.GetService<ClassShiftSegmentPlanner>() is { } planner
                ? await planner.ApplyAsync(appointment, work.Actor, cancellationToken: cancellationToken)
                : ClassFollowUpOutcome.GiveUp("Workforce is not enabled."),
            _ => ClassFollowUpOutcome.GiveUp("The work is not known."),
        };
    }
}

/// <summary>
/// What became of a piece of work on a class registration.
/// </summary>
internal enum ClassFollowUpOutcomeKind
{
    Done,
    Retry,
    GiveUp,
}

/// <summary>
/// What became of a piece of work on a class registration, and why.
/// </summary>
internal readonly record struct ClassFollowUpOutcome(ClassFollowUpOutcomeKind Kind, string Message)
{
    public static ClassFollowUpOutcome Done => new(ClassFollowUpOutcomeKind.Done, null);

    public static ClassFollowUpOutcome Retry(string message) => new(ClassFollowUpOutcomeKind.Retry, message);

    public static ClassFollowUpOutcome GiveUp(string message) => new(ClassFollowUpOutcomeKind.GiveUp, message);
}
