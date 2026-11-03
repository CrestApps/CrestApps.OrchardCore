namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Reads the secrets of a connection, such as a password, an API key or a client secret.
/// </summary>
/// <remarks>
/// A secret is stored protected with the tenant's data protection keys and is only read when a connector asks for it.
/// Never log a secret or put it in an exception message.
/// </remarks>
public interface IDataConnectorSecrets
{
    /// <summary>
    /// Returns a secret of the connection in clear text.
    /// </summary>
    /// <param name="name">The secret's name, such as <c>Password</c>.</param>
    /// <returns>The secret, or <see langword="null"/> when none is stored.</returns>
    string Get(string name);
}
