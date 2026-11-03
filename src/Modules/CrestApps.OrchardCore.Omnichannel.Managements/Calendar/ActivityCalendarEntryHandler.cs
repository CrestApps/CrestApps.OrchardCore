using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Keeps an activity's calendar event in step when the activity is rescheduled, cancelled or deleted.
/// </summary>
internal sealed class ActivityCalendarEntryHandler : CatalogEntryHandlerBase<OmnichannelActivity>
{
    private readonly ActivityCalendarService _calendarService;

    public ActivityCalendarEntryHandler(ActivityCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public override Task UpdatedAsync(UpdatedContext<OmnichannelActivity> context, CancellationToken cancellationToken = default)
    {
        return context.Model is null ? Task.CompletedTask : _calendarService.SyncEventAsync(context.Model, cancellationToken);
    }

    public override Task DeletedAsync(DeletedContext<OmnichannelActivity> context, CancellationToken cancellationToken = default)
    {
        return context.Model is null ? Task.CompletedTask : _calendarService.DeleteEventAsync(context.Model, cancellationToken);
    }
}
