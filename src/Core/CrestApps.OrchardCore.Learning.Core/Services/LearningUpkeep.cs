using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Telemetry;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The timed work of Learning: expires the enrollments whose access ended, and tells the observers once about access
/// that ends soon and about learners who stopped working on a course. Markers on the enrollment keep each notice from
/// being raised twice.
/// </summary>
public sealed class LearningUpkeep
{
    /// <summary>
    /// The enrollment property that records the end of access an access-ending notice was raised for.
    /// </summary>
    public const string AccessEndingMarker = "LearningAccessEndingRaised";

    /// <summary>
    /// The enrollment property that records the last activity an inactivity notice was raised for.
    /// </summary>
    public const string InactiveMarker = "LearningInactiveRaised";

    /// <summary>
    /// The enrollment property that records the due date a due-soon notice was raised for.
    /// </summary>
    public const string DueSoonMarker = "LearningDueSoonRaised";

    /// <summary>
    /// The enrollment property that records the due date an overdue notice was raised for.
    /// </summary>
    public const string OverdueMarker = "LearningOverdueRaised";

    private const int PageSize = 100;

    private readonly IEnrollmentService _enrollmentService;
    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly IEnumerable<IEnrollmentObserver> _observers;
    private readonly LearningUpkeepOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningUpkeep"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="writeGate">The per-enrollment write lock.</param>
    /// <param name="observers">The enrollment observers.</param>
    /// <param name="options">The upkeep options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LearningUpkeep(
        IEnrollmentService enrollmentService,
        EnrollmentStore enrollmentStore,
        EnrollmentWriteGate writeGate,
        IEnumerable<IEnrollmentObserver> observers,
        IOptions<LearningUpkeepOptions> options,
        IClock clock,
        ILogger<LearningUpkeep> logger)
    {
        _enrollmentService = enrollmentService;
        _enrollmentStore = enrollmentStore;
        _writeGate = writeGate;
        _observers = observers;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs the upkeep once.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var scope = LearningTelemetry.StartUpkeep();

        try
        {
            await RunCoreAsync(cancellationToken);
            scope.Succeed();
        }
        catch (Exception exception)
        {
            scope.Fail(exception);

            throw;
        }
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        await _enrollmentService.ExpireEndedAsync(cancellationToken);

        var now = _clock.UtcNow;

        if (_options.AccessEndingNoticeDays > 0)
        {
            foreach (var enrollment in await _enrollmentStore.ListAccessEndingAsync(now, now.AddDays(_options.AccessEndingNoticeDays), PageSize, cancellationToken))
            {
                await RaiseOnceAsync(enrollment.ItemId, AccessEndingMarker, current => Format(current.AccessEndsUtc), (observer, current) => observer.AccessEndingAsync(current, cancellationToken), cancellationToken);
            }
        }

        if (_options.DueSoonDays > 0)
        {
            foreach (var enrollment in await _enrollmentStore.ListDueAsync(now, now.AddDays(_options.DueSoonDays), PageSize, cancellationToken))
            {
                await RaiseOnceAsync(enrollment.ItemId, DueSoonMarker, current => Format(current.DueUtc), (observer, current) => observer.DueSoonAsync(current, cancellationToken), cancellationToken);
            }
        }

        foreach (var enrollment in await _enrollmentStore.ListDueAsync(DateTime.MinValue, now, PageSize, cancellationToken))
        {
            await RaiseOnceAsync(enrollment.ItemId, OverdueMarker, current => Format(current.DueUtc), (observer, current) => observer.OverdueAsync(current, cancellationToken), cancellationToken);
        }

        if (_options.InactiveDays > 0)
        {
            foreach (var enrollment in await _enrollmentStore.ListInactiveAsync(now.AddDays(-_options.InactiveDays), PageSize, cancellationToken))
            {
                await RaiseOnceAsync(enrollment.ItemId, InactiveMarker, current => Format(current.LastActivityUtc ?? current.CreatedUtc), (observer, current) => observer.InactiveAsync(current, cancellationToken), cancellationToken);
            }
        }
    }

    // Each notice is raised under the enrollment's own lock on a fresh read, so it never overwrites a change a learner or
    // an administrator made meanwhile; the marker is committed before the observers hear of it. A busy enrollment is left
    // for the next run.
    private Task<bool> RaiseOnceAsync(string enrollmentId, string markerName, Func<Enrollment, string> markerOf, Func<IEnrollmentObserver, Enrollment, Task> notify, CancellationToken cancellationToken)
        => _writeGate.RunAsync(
            enrollmentId,
            async notifications =>
            {
                var enrollment = await _enrollmentStore.FindFreshAsync(enrollmentId, cancellationToken);

                if (enrollment is null)
                {
                    return false;
                }

                var marker = markerOf(enrollment);

                if (Equals(enrollment.Properties.TryGetValue(markerName, out var raised) ? raised?.ToString() : null, marker))
                {
                    return false;
                }

                enrollment.Properties[markerName] = marker;
                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                notifications.Add(() => NotifyAsync(observer => notify(observer, enrollment)));

                return true;
            },
            () => false,
            cancellationToken);

    private static string Format(DateTime? value)
        => value?.ToString("O", CultureInfo.InvariantCulture);

    private async Task NotifyAsync(Func<IEnrollmentObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An enrollment observer {Observer} failed.", observer.GetType().Name);
            }
        }
    }
}
