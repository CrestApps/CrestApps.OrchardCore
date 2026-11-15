using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A customer waiting for a time to free up.
/// </summary>
public sealed class WaitlistEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the start of the times the customer can take.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the times the customer can take.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number.
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// Gets or sets the entry's state.
    /// </summary>
    public WaitlistStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the customer joined the list.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the customer was told a time freed up.
    /// </summary>
    public DateTime? NotifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user waiting, when they are signed in.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the dated session the person waits for; empty for a wait on any free time of the type.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the appointment held for the person when a place was offered to them.
    /// </summary>
    public string AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets when the offer runs out and passes to the next person.
    /// </summary>
    public DateTime? OfferExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets what the wait is for, such as a learner's enrollment.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id within <see cref="Source"/>.
    /// </summary>
    public string SourceId { get; set; }
}
