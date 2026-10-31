using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// A licence or certificate a person can hold, such as a gas safety certificate, that expires and is renewed.
/// </summary>
public sealed class Certification : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique certification name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who issues it.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets what the certification covers.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many months a certificate stays valid after it is issued, or <see langword="null"/> when it
    /// never expires. A record's own expiry date wins when one is entered.
    /// </summary>
    public int? ValidityMonths { get; set; }

    /// <summary>
    /// Gets or sets whether a record needs a copy of the certificate before it can be marked valid.
    /// </summary>
    public bool RequiresDocument { get; set; }

    /// <summary>
    /// Gets or sets how many days before expiry a reminder is raised, one reminder per entry (for example 60, 30 and 7).
    /// </summary>
    public IList<int> ReminderDays { get; set; } = [30];

    /// <summary>
    /// Gets or sets whether the certification can be recorded and required.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the certification was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the certification was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
