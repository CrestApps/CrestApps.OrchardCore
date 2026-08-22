namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Specifies the environment mode used by a payment gateway.
/// </summary>
public enum GatewayMode
{
    /// <summary>
    /// Uses the production payment gateway environment.
    /// </summary>
    Live,

    /// <summary>
    /// Uses the sandbox or test payment gateway environment.
    /// </summary>
    Testing,
}
