using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for an activity queue.
/// </summary>
public class QueueViewModel
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique queue name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queue description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the default priority.
    /// </summary>
    public InteractionPriority DefaultPriority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the SLA threshold in seconds.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int SlaThresholdSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the reservation timeout in seconds.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ReservationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the comma-separated skills required to receive work from the queue.
    /// </summary>
    public string RequiredSkills { get; set; }

    /// <summary>
    /// Gets or sets the inbound channel endpoint identifier mapped to this queue.
    /// </summary>
    public string InboundChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
