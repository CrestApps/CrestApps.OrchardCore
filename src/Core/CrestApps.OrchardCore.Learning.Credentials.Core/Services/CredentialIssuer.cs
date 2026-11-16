namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The site as the issuer of its credentials.
/// </summary>
/// <param name="BaseUrl">The site's base address with the tenant prefix, without a trailing slash.</param>
/// <param name="Name">The issuer's name, the site's name.</param>
public sealed record CredentialIssuer(string BaseUrl, string Name);
