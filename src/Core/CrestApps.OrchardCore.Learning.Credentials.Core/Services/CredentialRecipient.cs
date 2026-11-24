namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The person a credential is awarded to.
/// </summary>
/// <param name="UserId">The user id.</param>
/// <param name="Email">The e-mail address, which the credential names only as a salted hash.</param>
public sealed record CredentialRecipient(string UserId, string Email);
