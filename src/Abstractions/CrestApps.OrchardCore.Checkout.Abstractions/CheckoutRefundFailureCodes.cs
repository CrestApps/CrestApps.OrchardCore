namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The well-known failure codes a checkout stamps on a <see cref="Models.PaymentRefund"/> so downstream
/// services can reason about why a refund was flagged, without re-deriving the reason from free text.
/// </summary>
public static class CheckoutRefundFailureCodes
{
    /// <summary>
    /// The failure code stamped on a refund that a gateway reported with no matching local refund request
    /// (for example a refund issued from the provider dashboard), which an operator must review. When the
    /// refund also has no provider reference it is an identity-less charge-level aggregate observation.
    /// </summary>
    public const string RemoteRefundWithoutLocalRequest = "remote_refund_without_local_request";
}
