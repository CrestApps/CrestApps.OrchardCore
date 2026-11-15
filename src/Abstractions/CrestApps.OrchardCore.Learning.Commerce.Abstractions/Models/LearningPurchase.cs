using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Commerce.Models;

/// <summary>
/// What one paid order line granted, so a refund or a cancellation takes exactly that back.
/// </summary>
public sealed class LearningPurchase : CatalogItem
{
    public string OrderId { get; set; }

    public string LineId { get; set; }

    public string UserId { get; set; }

    public string AccountId { get; set; }

    public string ProductId { get; set; }

    public List<string> EnrollmentIds { get; set; } = [];

    public List<string> PathIds { get; set; } = [];

    public List<string> PoolIds { get; set; } = [];

    public DateTime GrantedUtc { get; set; }

    public DateTime? RevokedUtc { get; set; }
}
