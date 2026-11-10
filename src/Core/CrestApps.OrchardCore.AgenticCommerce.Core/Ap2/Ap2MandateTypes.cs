namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// The AP2 v0.2 mandate types (<c>vct</c>) and the JOSE <c>typ</c> values of delegation hops.
/// </summary>
public static class Ap2MandateTypes
{
    /// <summary>
    /// The closed checkout mandate type.
    /// </summary>
    public const string CheckoutMandate = "mandate.checkout.1";

    /// <summary>
    /// The open checkout mandate type, which delegates to an agent key with constraints.
    /// </summary>
    public const string OpenCheckoutMandate = "mandate.checkout.open.1";

    /// <summary>
    /// The closed payment mandate type.
    /// </summary>
    public const string PaymentMandate = "mandate.payment.1";

    /// <summary>
    /// The open payment mandate type.
    /// </summary>
    public const string OpenPaymentMandate = "mandate.payment.open.1";

    /// <summary>
    /// The <c>typ</c> of a terminal (closed) delegation hop.
    /// </summary>
    public const string TerminalHopType = "kb+sd-jwt";

    /// <summary>
    /// The <c>typ</c> of an intermediate delegation hop, which carries <c>cnf</c> for the next signer.
    /// </summary>
    public const string IntermediateHopType = "kb+sd-jwt+kb";

    /// <summary>
    /// The <c>typ</c> of a standard SD-JWT key binding JWT.
    /// </summary>
    public const string KeyBindingJwtType = "kb+jwt";

    internal static bool IsTerminalHop(string typ)
    {
        return typ is TerminalHopType or "kb-sd-jwt";
    }

    internal static bool IsIntermediateHop(string typ)
    {
        return typ is IntermediateHopType or "kb-sd-jwt+kb";
    }
}
