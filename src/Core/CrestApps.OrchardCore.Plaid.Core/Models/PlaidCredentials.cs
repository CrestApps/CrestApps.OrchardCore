namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// What every Plaid call needs: where to send it, and the client id and secret. Held in memory only.
/// </summary>
/// <param name="Environment">The environment.</param>
/// <param name="ClientId">The client id.</param>
/// <param name="Secret">The secret, in clear text.</param>
public sealed record PlaidCredentials(PlaidEnvironment Environment, string ClientId, string Secret)
{
    /// <summary>
    /// Gets the API's base address for the environment.
    /// </summary>
    public Uri BaseAddress
        => Environment == PlaidEnvironment.Production
            ? new Uri("https://production.plaid.com/")
            : new Uri("https://sandbox.plaid.com/");

    /// <inheritdoc />
    public override string ToString()
        => $"Plaid {Environment} ({ClientId})";
}
