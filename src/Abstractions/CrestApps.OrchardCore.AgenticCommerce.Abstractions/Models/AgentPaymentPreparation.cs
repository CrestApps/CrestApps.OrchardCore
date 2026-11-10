namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// How the store's checkout charges an agent's credential.
/// </summary>
public sealed class AgentPaymentPreparation
{
    /// <summary>
    /// Gets or sets a value indicating whether the credential can be charged.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the payment provider that charges it.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets what the provider needs.
    /// </summary>
    public IDictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets why it can't be charged.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Returns a preparation that refuses the credential.
    /// </summary>
    /// <param name="errorMessage">Why.</param>
    public static AgentPaymentPreparation Fail(string errorMessage)
        => new() { ErrorMessage = errorMessage };
}
