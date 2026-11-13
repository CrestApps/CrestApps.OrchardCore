using System.Globalization;
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
