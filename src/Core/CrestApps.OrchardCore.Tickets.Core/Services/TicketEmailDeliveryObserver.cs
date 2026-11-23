using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Shows on a ticket's timeline what became of the requester e-mail an entry carries (a reply): a notice while it
/// waits for another try, a "not delivered" notice once it is given up, and no notice once it is sent.
/// </summary>
public sealed class TicketEmailDeliveryObserver : ITemplatedEmailOutboxObserver
{
    private readonly ITimelineService _timeline;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailDeliveryObserver"/> class.
    /// </summary>
    /// <param name="timeline">The timeline the entries are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketEmailDeliveryObserver(ITimelineService timeline, IStringLocalizer<TicketEmailDeliveryObserver> stringLocalizer)
    {
        _timeline = timeline;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task DeliveredAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
        => MarkAsync(delivery, null, cancellationToken);

    /// <inheritdoc/>
    public Task RetryingAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
        => MarkAsync(delivery, S["Not delivered yet; trying again. {0}", delivery?.LastError], cancellationToken);

    /// <inheritdoc/>
    public Task AbandonedAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
        => MarkAsync(delivery, S["Not delivered to the requester: {0}", delivery?.LastError], cancellationToken);

    /// <summary>
    /// Sets or clears the delivery notice of a timeline entry.
    /// </summary>
    /// <param name="timeline">The timeline.</param>
    /// <param name="entryId">The entry, or <see langword="null"/> for a message that carries none.</param>
    /// <param name="notice">The notice, or <see langword="null"/> to clear it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the entry is changed.</returns>
    internal static async Task MarkEntryAsync(ITimelineService timeline, string entryId, string notice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return;
        }

        await timeline.UpdateAsync(entryId, changes =>
        {
            if (notice is null)
            {
                changes.Properties.Remove(TimelineConstants.Properties.Notice);
            }
            else
            {
                changes.Properties[TimelineConstants.Properties.Notice] = notice;
            }
        }, cancellationToken);
    }

    // Only a ticket's e-mails that carry an entry are marked.
    private Task MarkAsync(TemplatedEmailDelivery delivery, string notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (!string.Equals(delivery.Source.Kind, TicketsConstants.RecordKind, StringComparison.Ordinal)
            || delivery.Properties is null
            || !delivery.Properties.TryGetValue(TicketEmailOutbox.EntryIdProperty, out var entryId))
        {
            return Task.CompletedTask;
        }

        return MarkEntryAsync(_timeline, entryId, notice, cancellationToken);
    }
}
