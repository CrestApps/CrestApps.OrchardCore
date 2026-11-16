using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using OrchardCore.Entities;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Timeline.Notifications;

/// <summary>
/// Hides a mention from a user who may no longer read the entry, such as an internal note on a record moved where they
/// don't work.
/// </summary>
internal sealed class TimelineMentionVisibilityHandler : INotificationVisibilityHandler
{
    private readonly ITimelineAccessService _accessService;

    public TimelineMentionVisibilityHandler(ITimelineAccessService accessService)
    {
        _accessService = accessService;
    }

    /// <inheritdoc/>
    public async Task EvaluateAsync(IReadOnlyList<NotificationVisibilityContext> contexts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        var allowed = new Dictionary<(string Subject, TimelineVisibility Visibility), bool>();

        foreach (var context in contexts.Where(context => context.Notification.Type == TimelineConstants.MentionNotificationType))
        {
            if (!context.Notification.TryGet<TimelineMentionInfo>(out var info) || !RecordRef.TryParse(info.Subject, out var subject))
            {
                continue;
            }

            var key = (info.Subject, info.Visibility);

            if (!allowed.TryGetValue(key, out var canView))
            {
                canView = await _accessService.CanViewAsync(context.User, subject, info.Visibility, cancellationToken);
                allowed[key] = canView;
            }

            if (!canView)
            {
                context.Hide();
            }
        }
    }
}
