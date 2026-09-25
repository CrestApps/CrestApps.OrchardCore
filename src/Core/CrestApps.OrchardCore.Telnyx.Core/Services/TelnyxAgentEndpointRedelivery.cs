namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Where to ring a user's phone again after a leg to one of their credentials was refused as unavailable.
/// </summary>
/// <param name="Endpoint">The SIP address to ring instead.</param>
/// <param name="CredentialId">The credential that address belongs to.</param>
/// <param name="UnreachableCredentialId">The credential the refused leg rang, when it was one of the user's.</param>
public sealed record TelnyxAgentEndpointRedelivery(string Endpoint, string CredentialId, string UnreachableCredentialId);
