using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.MailingLists.Core.Coupons;

/// <summary>
/// The tenant's own secret of the mailing lists module, kept in a document of its own so neither a settings recipe nor
/// a settings export carries it.
/// </summary>
public sealed class MailingListsKeysDocument : Document
{
    /// <summary>
    /// Gets or sets the random 256-bit key, in Base64, that the welcome coupon's idempotency keys are made with. It is
    /// made once per tenant and never rotates: a new key would let an address get a second coupon.
    /// </summary>
    public string CouponKey { get; set; }
}
