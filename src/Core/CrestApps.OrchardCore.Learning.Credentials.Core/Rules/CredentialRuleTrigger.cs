using CrestApps.Core.Support;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Rules;

/// <summary>
/// Asks for the awards of every published definition with a rule of a kind on a target.
/// </summary>
public sealed class CredentialRuleTrigger
{
    private readonly ICredentialDefinitionCatalog _definitions;
    private readonly ICredentialAwardQueue _queue;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialRuleTrigger"/> class.
    /// </summary>
    /// <param name="definitions">The definitions.</param>
    /// <param name="queue">The award queue.</param>
    /// <param name="logger">The logger.</param>
    public CredentialRuleTrigger(
        ICredentialDefinitionCatalog definitions,
        ICredentialAwardQueue queue,
        ILogger<CredentialRuleTrigger> logger)
    {
        _definitions = definitions;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>
    /// Asks for the awards a rule earns. Never throws: a failure is logged and the learner's change stands.
    /// </summary>
    /// <param name="kind">The rule kind.</param>
    /// <param name="targetId">What the rule watches.</param>
    /// <param name="userId">The person.</param>
    /// <param name="sourceId">What earned it, such as the completed enrollment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task TriggerAsync(string kind, string targetId, string userId, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        try
        {
            foreach (var definition in await _definitions.ListByRuleAsync(kind, targetId, cancellationToken))
            {
                _queue.Enqueue(new CredentialAwardRequest
                {
                    DefinitionId = definition.DefinitionId,
                    UserId = userId,
                    RuleKind = kind,
                    SourceId = sourceId,
                });
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Finding the credentials a {Kind} rule on '{TargetId}' awards failed.", kind.SanitizeLogValue(), targetId.SanitizeLogValue());
        }
    }
}
