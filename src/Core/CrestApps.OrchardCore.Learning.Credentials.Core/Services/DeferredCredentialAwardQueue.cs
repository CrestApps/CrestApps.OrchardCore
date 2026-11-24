using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Runs each award as an Orchard Core deferred task, after the request that triggered it is saved.
/// </summary>
public sealed class DeferredCredentialAwardQueue : ICredentialAwardQueue
{
    /// <inheritdoc/>
    public void Enqueue(CredentialAwardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;

            try
            {
                var result = await services.GetRequiredService<ICredentialAwardService>().AwardAsync(request);

                if (!result.Succeeded)
                {
                    services.GetRequiredService<ILogger<DeferredCredentialAwardQueue>>().LogWarning(
                        "The credential '{DefinitionId}' was not awarded to the user '{UserId}' ({RuleKind} {SourceId}): {ErrorCode}.",
                        request.DefinitionId,
                        request.UserId,
                        request.RuleKind,
                        request.SourceId,
                        result.ErrorCode);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                services.GetRequiredService<ILogger<DeferredCredentialAwardQueue>>().LogError(
                    exception,
                    "Awarding the credential '{DefinitionId}' to the user '{UserId}' ({RuleKind} {SourceId}) failed.",
                    request.DefinitionId,
                    request.UserId,
                    request.RuleKind,
                    request.SourceId);
            }
        });
    }
}
