using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A shared device workers clock in on with their badge number and PIN. It opens a private address.
/// </summary>
public sealed class TimeClockKiosk : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name, such as the place it stands.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team whose workers use it; empty for anyone.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the hash of the address's secret.
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it was last used.
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
