using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="TenantProvisioningJob"/>.
/// </summary>
public sealed class TenantProvisioningJobIndex : CatalogItemIndex
{
    /// <summary>
    /// The checkout the tenant was bought through.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// The subscription the tenant belongs to.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// The buyer.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The Orchard Core tenant name.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// The current state of the job.
    /// </summary>
    public TenantProvisioningStatus Status { get; set; }

    /// <summary>
    /// The UTC time the job may next be attempted.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// The UTC time the job was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TenantProvisioningJob"/> documents to <see cref="TenantProvisioningJobIndex"/> rows.
/// </summary>
public sealed class TenantProvisioningJobIndexProvider : IndexProvider<TenantProvisioningJob>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningJobIndexProvider"/> class.
    /// </summary>
    public TenantProvisioningJobIndexProvider()
    {
        CollectionName = SubscriptionConstants.TenantProvisioningCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TenantProvisioningJob> context)
    {
        context.For<TenantProvisioningJobIndex>()
            .Map(job => new TenantProvisioningJobIndex
            {
                ItemId = job.ItemId,
                CheckoutSessionId = job.CheckoutSessionId,
                SubscriptionId = job.SubscriptionId,
                OwnerId = job.OwnerId,
                TenantName = job.TenantName,
                Status = job.Status,
                NextAttemptUtc = job.NextAttemptUtc,
                CreatedUtc = job.CreatedUtc,
            });
    }
}
