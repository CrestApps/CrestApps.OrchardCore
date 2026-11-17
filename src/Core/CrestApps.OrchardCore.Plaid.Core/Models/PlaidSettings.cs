namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// The site's Plaid settings. The secret itself is never here: only the name of the Orchard secret that holds it.
/// </summary>
public class PlaidSettings
{
    /// <summary>
    /// Gets or sets the environment.
    /// </summary>
    public PlaidEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the client id from the Plaid dashboard. It names the Plaid account and is not a credential.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the name of the Orchard secret (a text secret) that holds the Plaid secret of the environment.
    /// </summary>
    public string SecretName { get; set; }

    /// <summary>
    /// Returns whether the settings name a client id and a secret.
    /// </summary>
    /// <returns><see langword="true"/> when both are set.</returns>
    public bool IsConfigured()
        => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(SecretName);
}
