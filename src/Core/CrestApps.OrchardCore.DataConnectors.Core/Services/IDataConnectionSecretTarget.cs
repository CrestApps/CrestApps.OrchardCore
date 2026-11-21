using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Says where a connector sends a connection's stored secrets: its server and the way it signs in. A stored secret was
/// given for that place only, so when an edit or a recipe changes it, the stored secrets are dropped and must be entered
/// again; otherwise a user who may edit connections could point one at a server of their own and collect the password
/// from the sign-in. The HTTP connector has its own rule; a connector that keeps secrets registers one of these.
/// </summary>
public interface IDataConnectionSecretTarget
{
    /// <summary>
    /// Gets the name of the connector this applies to.
    /// </summary>
    string Connector { get; }

    /// <summary>
    /// Returns a value that changes whenever the connection would send its secrets somewhere else or sign in another way,
    /// such as its server and authentication.
    /// </summary>
    /// <param name="connection">The connection.</param>
    string Describe(DataConnection connection);
}
