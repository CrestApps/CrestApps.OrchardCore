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
    private readonly IEnumerable<IEnrollmentObserver> _observers;
    private readonly LearningUpkeepOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningUpkeep"/> class.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="observers">The enrollment observers.</param>
    /// <param name="options">The upkeep options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LearningUpkeep(
        IEnrollmentService enrollmentService,
        EnrollmentStore enrollmentStore,
        IEnumerable<IEnrollmentObserver> observers,
        IOptions<LearningUpkeepOptions> options,
        IClock clock,
        ILogger<LearningUpkeep> logger)
    {
        _enrollmentService = enrollmentService;
        _enrollmentStore = enrollmentStore;
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
                var marker = enrollment.AccessEndsUtc?.ToString("O", CultureInfo.InvariantCulture);

                if (Equals(enrollment.Properties.TryGetValue(AccessEndingMarker, out var raised) ? raised?.ToString() : null, marker))
                {
                    continue;
                }

                enrollment.Properties[AccessEndingMarker] = marker;
                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                await NotifyAsync(observer => observer.AccessEndingAsync(enrollment, cancellationToken));
            }
        }

        if (_options.DueSoonDays > 0)
        {
            foreach (var enrollment in await _enrollmentStore.ListDueAsync(now, now.AddDays(_options.DueSoonDays), PageSize, cancellationToken))
            {
                await RaiseOnceAsync(enrollment, DueSoonMarker, enrollment.DueUtc, observer => observer.DueSoonAsync(enrollment, cancellationToken), cancellationToken);
            }
        }

        foreach (var enrollment in await _enrollmentStore.ListDueAsync(DateTime.MinValue, now, PageSize, cancellationToken))
        {
            await RaiseOnceAsync(enrollment, OverdueMarker, enrollment.DueUtc, observer => observer.OverdueAsync(enrollment, cancellationToken), cancellationToken);
        }

        if (_options.InactiveDays > 0)
        {
            foreach (var enrollment in await _enrollmentStore.ListInactiveAsync(now.AddDays(-_options.InactiveDays), PageSize, cancellationToken))
            {
                var marker = (enrollment.LastActivityUtc ?? enrollment.CreatedUtc).ToString("O", CultureInfo.InvariantCulture);

                if (Equals(enrollment.Properties.TryGetValue(InactiveMarker, out var raised) ? raised?.ToString() : null, marker))
                {
                    continue;
                }

                enrollment.Properties[InactiveMarker] = marker;
                await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
                await NotifyAsync(observer => observer.InactiveAsync(enrollment, cancellationToken));
            }
        }
    }

    private async Task RaiseOnceAsync(Enrollment enrollment, string markerName, DateTime? value, Func<IEnrollmentObserver, Task> notify, CancellationToken cancellationToken)
    {
        var marker = value?.ToString("O", CultureInfo.InvariantCulture);

        if (Equals(enrollment.Properties.TryGetValue(markerName, out var raised) ? raised?.ToString() : null, marker))
        {
            return;
        }

        enrollment.Properties[markerName] = marker;
        await _enrollmentStore.SaveAsync(enrollment, cancellationToken);
        await NotifyAsync(notify);
    }

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
