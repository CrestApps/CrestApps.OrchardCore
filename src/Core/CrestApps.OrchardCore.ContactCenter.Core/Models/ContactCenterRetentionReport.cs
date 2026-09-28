namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Reports the outcome of one retention cycle across every registered policy.
/// </summary>
public sealed class ContactCenterRetentionReport
{
    /// <summary>
    /// Gets the per-entity results, one for every registered policy including those whose purging is disabled.
    /// </summary>
    public IList<ContactCenterEntityRetentionResult> Entities { get; } = [];

    /// <summary>
    /// Gets the total number of records purged across every entity.
    /// </summary>
    public int TotalPurged => Entities.Sum(entity => entity.PurgedCount);

    /// <summary>
    /// Gets a value indicating whether any entity still had expired records when the cycle stopped. A cycle
    /// that ends with this set has not returned the database to steady state and the budget must be raised.
    /// </summary>
    public bool WorkRemains => Entities.Any(entity => entity.WorkRemains);
}
