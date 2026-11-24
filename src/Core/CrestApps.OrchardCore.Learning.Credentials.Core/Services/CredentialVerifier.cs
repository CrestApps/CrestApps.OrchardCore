using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Checks a credential on the server, as a verifier would: the JSON Web Token's signature against the published key
/// named by its <c>kid</c>, its issuer and id against the credential as issued, its expiry, and its revocation bit read
/// from the status list.
/// </summary>
public sealed class CredentialVerifier
{
    private readonly CredentialSigningKeys _keys;
    private readonly CredentialStatusLists _statusLists;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialVerifier"/> class.
    /// </summary>
    /// <param name="keys">The credentials key set.</param>
    /// <param name="statusLists">The status lists.</param>
    /// <param name="clock">The clock.</param>
    public CredentialVerifier(CredentialSigningKeys keys, CredentialStatusLists statusLists, IClock clock)
    {
        _keys = keys;
        _statusLists = statusLists;
        _clock = clock;
    }

    /// <summary>
    /// Checks an award's credential.
    /// </summary>
    /// <param name="award">The award, or <see langword="null"/> when none has the code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<CredentialVerificationResult> VerifyAsync(CredentialAward award, CancellationToken cancellationToken = default)
    {
        if (award is null)
        {
            return new CredentialVerificationResult { Outcome = CredentialVerificationOutcome.NotFound };
        }

        var result = new CredentialVerificationResult
        {
            Award = award,
            Revoked = await _statusLists.IsRevokedAsync(award.StatusListNumber, award.StatusListIndex, cancellationToken),
        };

        if (award.ErasedUtc is not null || string.IsNullOrEmpty(award.Jwt) || string.IsNullOrEmpty(award.Credential))
        {
            result.Erased = true;
            result.Outcome = CredentialVerificationOutcome.Revoked;

            return result;
        }

        var (header, payload) = CredentialJws.Read(award.Jwt);
        var key = await _keys.FindPublishedKeyAsync(Text(header?["kid"]), cancellationToken);
        var issued = JsonNode.Parse(award.Credential);
        var issuerId = issued?["issuer"] is JsonObject issuer ? Text(issuer["id"]) : Text(issued?["issuer"]);

        result.SignatureValid = key is not null &&
            CredentialJws.Verify(award.Jwt, key) &&
            string.Equals(Text(payload?["jti"]), Text(issued?["id"]), StringComparison.Ordinal) &&
            string.Equals(Text(payload?["iss"]), issuerId, StringComparison.Ordinal);

        if (payload?["exp"] is JsonValue exp && exp.GetValueKind() == JsonValueKind.Number)
        {
            result.ValidUntilUtc = DateTimeOffset.FromUnixTimeSeconds(exp.GetValue<long>()).UtcDateTime;
            result.Expired = result.ValidUntilUtc <= _clock.UtcNow;
        }

        result.Outcome = !result.SignatureValid
            ? CredentialVerificationOutcome.Invalid
            : result.Revoked
                ? CredentialVerificationOutcome.Revoked
                : result.Expired
                    ? CredentialVerificationOutcome.Expired
                    : CredentialVerificationOutcome.Valid;

        return result;
    }

    private static string Text(JsonNode node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
