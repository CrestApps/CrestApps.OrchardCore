namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// What a check line holds.
/// </summary>
public sealed class MicrLineRequest
{
    /// <summary>
    /// Gets or sets the bank's routing number.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the account number, in clear text: the check line prints it.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the check number.
    /// </summary>
    public long CheckNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the check number prints left of the routing number (business checks) rather than after the
    /// account number (personal checks).
    /// </summary>
    public bool UseAuxiliaryOnUs { get; set; } = true;
}
