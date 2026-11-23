using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Emails;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="CouponCode"/>.
/// </summary>
public sealed class CouponCodeIndex : MapIndex
{
    /// <summary>
    /// The code's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// The identifier of the coupon it was generated for.
    /// </summary>
    public string CouponId { get; set; }

    /// <summary>
    /// The code, normalized to upper case, as <see cref="CouponIndex.NormalizedCode"/>.
    /// </summary>
    public string NormalizedCode { get; set; }

    /// <summary>
    /// How many times it has been used.
    /// </summary>
    public int RedemptionCount { get; set; }

    /// <summary>
    /// The UTC time it was generated.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The key the code was issued under with <c>ICouponIssuer</c>, or <see langword="null"/> for a code generated in
    /// bulk. At most <see cref="CheckoutConstants.MaxCouponIdempotencyKeyLength"/> characters.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The e-mail address the code is bound to, normalized with <see cref="EmailAddresses.Normalize"/>, or
    /// <see langword="null"/> when the coupon decides who may use it. A privacy request finds a person's codes by it. At
    /// most <see cref="EmailAddresses.MaxLength"/> characters.
    /// </summary>
    public string NormalizedEmail { get; set; }
}

/// <summary>
/// Maps <see cref="CouponCode"/> documents to <see cref="CouponCodeIndex"/> rows.
/// </summary>
public sealed class CouponCodeIndexProvider : IndexProvider<CouponCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CouponCodeIndexProvider"/> class.
    /// </summary>
    public CouponCodeIndexProvider()
    {
        CollectionName = CheckoutConstants.CouponCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CouponCode> context)
    {
        context.For<CouponCodeIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(code => new CouponCodeIndex
            {
                ItemId = code.ItemId,
                CouponId = code.CouponId,
                NormalizedCode = code.Code?.ToUpperInvariant(),
                RedemptionCount = code.RedemptionCount,
                CreatedUtc = code.CreatedUtc,
                IdempotencyKey = string.IsNullOrWhiteSpace(code.IdempotencyKey) ? null : code.IdempotencyKey.Trim(),
                NormalizedEmail = EmailAddresses.Normalize(code.CustomerEmail) is { Length: <= EmailAddresses.MaxLength } email
                    ? email
                    : null,
            });
    }
}
