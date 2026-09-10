using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for an activity queue group.
/// </summary>
public class ActivityQueueGroupViewModel
{
    /// <summary>
    /// Gets or sets the queue-group identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique queue-group name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queue-group description.
    /// </summary>
    public string Description { get; set; }
}
