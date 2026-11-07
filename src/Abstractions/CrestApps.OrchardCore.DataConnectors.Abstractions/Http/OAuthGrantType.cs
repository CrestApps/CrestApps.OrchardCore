namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// The grants used to get a token.
/// </summary>
public enum OAuthGrantType
{
    /// <summary>
    /// The client's own credentials (machine to machine).
    /// </summary>
    ClientCredentials,

    /// <summary>
    /// A user's name and password, for servers that still support the resource owner password grant.
    /// </summary>
    Password,
}
