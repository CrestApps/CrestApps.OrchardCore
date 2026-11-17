namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// The Plaid environment the site talks to.
/// </summary>
public enum PlaidEnvironment
{
    /// <summary>
    /// Test banks and test credentials, free.
    /// </summary>
    Sandbox,

    /// <summary>
    /// Real banks.
    /// </summary>
    Production,
}
