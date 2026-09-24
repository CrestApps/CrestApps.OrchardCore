using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Reads a period's offer events from the event log.
/// </summary>
/// <remarks>
/// Offers are recorded against their reservation. An accepted offer used to be recorded against its interaction
/// instead, so the reports that read offers by reservation never saw one accepted: every answered call looked like an
/// offer that was never settled. Those older acceptances are read from where they were written, beside the rest.
/// </remarks>
internal static class OfferEventReader
{
    /// <summary>
    /// Reads the offer events of a period.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The offer events.</returns>
    public static async Task<IReadOnlyList<InteractionEvent>> ReadAsync(
        IInteractionEventStore eventStore,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        var offers = await eventStore.GetByAggregateWindowAsync(nameof(ActivityReservation), CallHandlingMetrics.OfferEventTypes, null, fromUtc, toUtc, cancellationToken);
        var olderAcceptances = await eventStore.GetByAggregateWindowAsync(
            nameof(Interaction),
            [ContactCenterConstants.Events.OfferAccepted],
            null,
            fromUtc,
            toUtc,
            cancellationToken);

        return [.. offers, .. olderAcceptances];
    }
}
