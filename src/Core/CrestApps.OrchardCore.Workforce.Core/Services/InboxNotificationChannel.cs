using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Keeps every notification in the worker's portal inbox. It can't be turned off.
/// </summary>
internal sealed class InboxNotificationChannel : IWorkforceNotificationChannel, IWorkforceInbox
{
    private readonly WorkforceInboxStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public InboxNotificationChannel(WorkforceInboxStore store, IClock clock, IStringLocalizer<InboxNotificationChannel> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public string Name => "Inbox";

    public string DisplayName => S["My Work inbox"];

    public bool CanBeTurnedOff => false;

    public Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default)
    {
        return _store.CreateAsync(new WorkforceInboxItem
        {
            ItemId = UniqueId.GenerateId(),
            WorkerId = worker.ItemId,
            Kind = notification.Kind,
            Title = notification.Title,
            Body = notification.Body,
            Url = notification.Url,
            CreatedUtc = _clock.UtcNow,
        }, cancellationToken).AsTask();
    }

    public Task<(int Count, int Unread, IReadOnlyList<WorkforceInboxItem> Items)> PageAsync(string workerId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _store.PageAsync(workerId, skip, take, cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var unread = await _store.ListUnreadAsync(workerId, cancellationToken);

        foreach (var item in unread)
        {
            item.ReadUtc = now;
            await _store.UpdateAsync(item, cancellationToken);
        }

        return unread.Count;
    }
}
