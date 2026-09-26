using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The offer reports read offers by their reservation. An acceptance recorded before it was filed there, against the
/// interaction, is read too, or every call answered in that time shows as an offer nobody settled.
/// </summary>
public sealed class OfferEventReaderTests
{
    private static readonly DateTime _fromUtc = new(2026, 9, 24, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _toUtc = _fromUtc.AddDays(1);

    [Fact]
    public async Task ReadAsync_ReadsOffersByReservation_AndOlderAcceptancesByInteraction()
    {
        // Arrange
        var presented = new InteractionEvent { ItemId = "e1", EventType = ContactCenterConstants.Events.OfferPresented, AggregateType = nameof(ActivityReservation) };
        var olderAcceptance = new InteractionEvent { ItemId = "e2", EventType = ContactCenterConstants.Events.OfferAccepted, AggregateType = nameof(Interaction) };
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(nameof(ActivityReservation), CallHandlingMetrics.OfferEventTypes, null, _fromUtc, _toUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync([presented]);
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                nameof(Interaction),
                It.Is<IEnumerable<string>>(types => types.SequenceEqual(new[] { ContactCenterConstants.Events.OfferAccepted })),
                null,
                _fromUtc,
                _toUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([olderAcceptance]);

        // Act
        var offers = await OfferEventReader.ReadAsync(eventStore.Object, _fromUtc, _toUtc, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["e1", "e2"], offers.Select(offer => offer.ItemId));
    }
}
