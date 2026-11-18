using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Commerce.Models;

/// <summary>
/// What one paid order line buys and what it granted, so a refund or a cancellation takes exactly that back. The order
/// event records it as pending inside the checkout's unit of work; it is fulfilled after the checkout committed.
/// </summary>
public sealed class LearningPurchase : CatalogItem
{
    public string OrderId { get; set; }

    public string LineId { get; set; }

    public string UserId { get; set; }

    public string AccountId { get; set; }

    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets where the purchase stands.
    /// </summary>
    public LearningPurchaseState State { get; set; }

    /// <summary>
    /// Gets or sets what the line buys, as it was when it was paid.
    /// </summary>
    public List<LearningPurchaseGrant> Grants { get; set; } = [];

    public List<string> EnrollmentIds { get; set; } = [];

    public List<string> PathIds { get; set; } = [];

    public List<string> PoolIds { get; set; } = [];

    /// <summary>
    /// Gets or sets why a grant could not be made, for staff; empty when everything was granted.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets whether a full refund or a cancellation asked for the grants back.
    /// </summary>
    public bool RevokeRequested { get; set; }

    /// <summary>
    /// Gets or sets why the grants are taken back.
    /// </summary>
    public string RevokeReason { get; set; }

    /// <summary>
    /// Gets or sets when the line was paid.
    /// </summary>
    public DateTime GrantedUtc { get; set; }

    /// <summary>
    /// Gets or sets when everything the line buys was granted.
    /// </summary>
    public DateTime? FulfilledUtc { get; set; }

    /// <summary>
    /// Gets or sets when every grant was taken back.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }
}

/// <summary>
/// One thing a paid line grants, copied from the product's <see cref="LearningProductGrant"/> when it was paid.
/// </summary>
public sealed class LearningPurchaseGrant
{
    public LearningProductGrantKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the course or learning path.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the title of the course or path, for a licence pool and for staff.
    /// </summary>
    public string Title { get; set; }

    public int? AccessDays { get; set; }

    /// <summary>
    /// Gets or sets the seats the whole line buys, for <see cref="LearningProductGrantKind.Seats"/>.
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Gets or sets whether the product said buying it enrolls people in a course that needs a key or an approval.
    /// </summary>
    public bool EnrollsWithoutKeyOrApproval { get; set; }

    /// <summary>
    /// Gets or sets whether the grant was made.
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    /// Gets or sets why the grant can not be made.
    /// </summary>
    public string Error { get; set; }
}

/// <summary>
/// Where a learning purchase stands.
/// </summary>
public enum LearningPurchaseState
{
    /// <summary>
    /// Paid, and waiting to be granted.
    /// </summary>
    Pending,

    /// <summary>
    /// Everything was granted.
    /// </summary>
    Fulfilled,

    /// <summary>
    /// A grant can not be made; the reason is on the purchase and on the order.
    /// </summary>
    Failed,

    /// <summary>
    /// Every grant was taken back after a full refund or a cancellation.
    /// </summary>
    Revoked,
}
