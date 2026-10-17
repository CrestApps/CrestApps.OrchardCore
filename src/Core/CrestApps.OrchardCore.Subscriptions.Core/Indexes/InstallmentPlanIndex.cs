using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// The columns installment plans are listed, searched and swept by.
/// </summary>
public sealed class InstallmentPlanIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets what the plan pays for.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the plan's status.
    /// </summary>
    public InstallmentPlanStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the plan total.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets when the next payment still to be received falls due.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the plan was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the plan pays for.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the plan pays for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the outstanding transaction the plan replaces.
    /// </summary>
    public string SourceTransactionId { get; set; }
}

/// <summary>
/// Maps <see cref="InstallmentPlan"/> records to <see cref="InstallmentPlanIndex"/>.
/// </summary>
public sealed class InstallmentPlanIndexProvider : IndexProvider<InstallmentPlan>
{
    /// <summary>
    /// The longest reference type or reference identifier the index holds.
    /// </summary>
    public const int ReferenceMaxLength = 255;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanIndexProvider"/> class.
    /// </summary>
    public InstallmentPlanIndexProvider()
    {
        CollectionName = SubscriptionConstants.InstallmentPlanCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<InstallmentPlan> context)
    {
        context.For<InstallmentPlanIndex>()
            .Map(plan => new InstallmentPlanIndex
            {
                ItemId = plan.ItemId,
                Title = Truncate(plan.Title, 255),
                OwnerId = plan.OwnerId,
                CustomerName = Truncate(plan.CustomerName, 255),
                CustomerEmail = Truncate(plan.CustomerEmail, 255),
                Status = plan.Status,
                Currency = plan.Currency,
                TotalAmount = plan.TotalAmount,
                NextDueUtc = plan.NextPayment?.DueUtc,
                CreatedUtc = plan.CreatedUtc,
                ReferenceType = Truncate(plan.ReferenceType, ReferenceMaxLength),
                ReferenceId = Truncate(plan.ReferenceId, ReferenceMaxLength),
                SourceTransactionId = plan.SourceTransactionId,
            });
    }

    // The columns are bounded, and a longer value must not stop the plan from being saved. A query truncates the value
    // it looks for the same way, so it still finds the row.
    internal static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length];
}
