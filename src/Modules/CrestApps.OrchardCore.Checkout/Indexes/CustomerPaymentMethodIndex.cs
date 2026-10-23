using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Indexes;

/// <summary>
/// The queryable projection of a <see cref="CustomerPaymentMethod"/>.
/// </summary>
public sealed class CustomerPaymentMethodIndex : MapIndex
{
    public string ItemId { get; set; }

    public string OwnerId { get; set; }

    public string ProviderKey { get; set; }

    public string PaymentMethodReference { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="CustomerPaymentMethod"/> documents to <see cref="CustomerPaymentMethodIndex"/> rows.
/// </summary>
public sealed class CustomerPaymentMethodIndexProvider : IndexProvider<CustomerPaymentMethod>
{
    public override void Describe(DescribeContext<CustomerPaymentMethod> context)
    {
        context.For<CustomerPaymentMethodIndex>()
            .Map(method => new CustomerPaymentMethodIndex
            {
                ItemId = method.ItemId,
                OwnerId = method.OwnerId,
                ProviderKey = method.PaymentMethod?.ProviderKey,
                PaymentMethodReference = method.PaymentMethod?.PaymentMethodReference,
                IsDefault = method.IsDefault,
                CreatedUtc = method.CreatedUtc,
            });
    }
}
