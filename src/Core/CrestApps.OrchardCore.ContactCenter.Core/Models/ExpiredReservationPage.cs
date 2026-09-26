namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single keyset page of expired reservations returned by the reservation store.
/// The page carries an opaque cursor that the caller passes back to fetch the next page, so paging
/// stays correct even when reservations are concurrently expired or created while the backlog drains.
/// </summary>
public sealed class ExpiredReservationPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiredReservationPage"/> class.
    /// </summary>
    /// <param name="reservations">The expired reservations in this page.</param>
    /// <param name="nextAfterExpiresUtc">The expiry timestamp of the last index row in the page, or <see langword="null"/> when the backlog is exhausted.</param>
    /// <param name="nextAfterDocumentId">The document identifier of the last index row in the page. Only meaningful when <paramref name="nextAfterExpiresUtc"/> is not <see langword="null"/>.</param>
    public ExpiredReservationPage(
        IReadOnlyList<ActivityReservation> reservations,
        DateTime? nextAfterExpiresUtc,
        long nextAfterDocumentId)
    {
        Reservations = reservations;
        NextAfterExpiresUtc = nextAfterExpiresUtc;
        NextAfterDocumentId = nextAfterDocumentId;
    }

    /// <summary>
    /// Gets the expired reservations contained in this page. This may contain fewer entries than the
    /// underlying index page when documents were concurrently deleted between reading the index and loading them.
    /// </summary>
    public IReadOnlyList<ActivityReservation> Reservations { get; }

    /// <summary>
    /// Gets the expiry timestamp component of the keyset cursor pointing just past the last index row in
    /// this page, or <see langword="null"/> when the page was not full and the backlog is exhausted.
    /// </summary>
    public DateTime? NextAfterExpiresUtc { get; }

    /// <summary>
    /// Gets the document identifier component of the keyset cursor pointing just past the last index row in
    /// this page. Only meaningful when <see cref="NextAfterExpiresUtc"/> is not <see langword="null"/>.
    /// </summary>
    public long NextAfterDocumentId { get; }

    /// <summary>
    /// Gets a value indicating whether another page may be available, meaning the caller should continue
    /// draining using <see cref="NextAfterExpiresUtc"/> and <see cref="NextAfterDocumentId"/> as the cursor.
    /// </summary>
    public bool HasMore => NextAfterExpiresUtc.HasValue;
}
