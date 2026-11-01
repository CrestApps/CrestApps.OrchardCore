using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Tells a worker's line manager when the worker's attendance points reach their policy's threshold.
/// </summary>
internal sealed class ManagerPointsNotice : IAttendancePointsHandler
{
    private readonly IWorkforceNotifier _notifier;

    internal readonly IStringLocalizer S;

    public ManagerPointsNotice(IWorkforceNotifier notifier, IStringLocalizer<ManagerPointsNotice> stringLocalizer)
    {
        _notifier = notifier;
        S = stringLocalizer;
    }

    public Task ThresholdReachedAsync(WorkerProfile worker, AttendanceException exception, decimal points, TimeClockPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        if (string.IsNullOrEmpty(worker.ManagerWorkerId))
        {
            return Task.CompletedTask;
        }

        return _notifier.NotifyAsync(new WorkforceNotification
        {
            WorkerId = worker.ManagerWorkerId,
            Kind = WorkforceNotificationKind.Attendance,
            Title = S["{0} reached {1} attendance points", worker.DisplayName, points.ToString("0.##", CultureInfo.CurrentCulture)],
            Body = S["Their latest exception was on {0}.", exception.Date.ToString("D", CultureInfo.CurrentCulture)],
            Url = "~/my-work/approvals",
        }, cancellationToken);
    }
}
