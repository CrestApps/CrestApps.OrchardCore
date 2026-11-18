using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Finds and revokes the awards that rest on a source: the awards a rule of a kind made from a record, such as the
/// certificate of a certification issued rule or the attempt of a quiz passed rule. The rules call it when what an award
/// rests on goes away, so the verification page and the status list stop showing the award as valid.
/// </summary>
public sealed class CredentialSourceRevoker
{
    private readonly CredentialAwardStore _store;
    private readonly ICredentialAwardService _awards;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialSourceRevoker"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="awards">The award service, which revokes.</param>
    /// <param name="logger">The logger.</param>
    public CredentialSourceRevoker(CredentialAwardStore store, ICredentialAwardService awards, ILogger<CredentialSourceRevoker> logger)
    {
        _store = store;
        _awards = awards;
        _logger = logger;
    }

    /// <summary>
    /// Lists the awards a rule of a kind made from a source that are not revoked.
    /// </summary>
    /// <param name="ruleKind">The rule kind.</param>
    /// <param name="sourceId">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The awards.</returns>
    public async Task<IReadOnlyList<CredentialAward>> ListActiveAsync(string ruleKind, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ruleKind) || string.IsNullOrEmpty(sourceId))
        {
            return [];
        }

        return (await _store.ListBySourceAsync(ruleKind, sourceId, cancellationToken)).Where(award => !award.IsRevoked).ToArray();
    }

    /// <summary>
    /// Revokes every award a rule of a kind made from a source. Never throws: an award that cannot be revoked now is
    /// logged, and the change that went away stands.
    /// </summary>
    /// <param name="ruleKind">The rule kind.</param>
    /// <param name="sourceId">The source.</param>
    /// <param name="actor">Who made the change that revokes it, or <see langword="null"/>.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many awards were revoked.</returns>
    public async Task<int> RevokeAsync(string ruleKind, string sourceId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var revoked = 0;

        try
        {
            foreach (var award in await ListActiveAsync(ruleKind, sourceId, cancellationToken))
            {
                var result = await _awards.RevokeAsync(award.ItemId, actor, reason, cancellationToken);

                if (result.Succeeded)
                {
                    revoked++;
                }
                else if (result.ErrorCode != CredentialAwardErrors.AlreadyRevoked)
                {
                    _logger.LogError("A credential award that rests on a {RuleKind} source that went away could not be revoked: {ErrorCode}. Revoke award '{AwardId}' by hand.", ruleKind, result.ErrorCode, award.ItemId);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Revoking the credential awards that rest on a {RuleKind} source failed.", ruleKind);
        }

        return revoked;
    }
}
