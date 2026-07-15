using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to enforce provider command idempotency and to query commands that are
/// due for dispatch, reconciliation, or lease reclamation.
/// </summary>
public sealed class ProviderCommandIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the stable idempotency key. It is unique per tenant so a command is never duplicated.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets the canonical provider technical name the command targets.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle status of the command.
    /// </summary>
    public ProviderCommandStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing fence token.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the interaction identifier the command relates to, when applicable.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next dispatch or reconciliation attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current claim lease expires.
    /// </summary>
    public DateTime LeaseExpiresUtc { get; set; }
}
