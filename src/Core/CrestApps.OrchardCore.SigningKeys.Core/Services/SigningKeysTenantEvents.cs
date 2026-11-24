using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.SigningKeys.Core.Services;

/// <summary>
/// Makes (or imports) the first key of every registered key set when the tenant starts, in a scope of its own after the
/// activation. Orchard Core Secrets saves a secret in a child scope with its own transaction, which on SQLite waits
/// forever behind a request that already wrote in its own transaction; making the keys here keeps that from happening
/// on a request (an order, a launch) that needs a key for the first time.
/// </summary>
public sealed class SigningKeysTenantEvents : ModularTenantEvents
{
    /// <inheritdoc />
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var ring = services.GetRequiredService<ISigningKeyRing>();
            var options = services.GetRequiredService<IOptions<SigningKeyOptions>>().Value;
            var logger = services.GetRequiredService<ILogger<SigningKeysTenantEvents>>();

            foreach (var keySet in options.KeySets.Keys)
            {
                try
                {
                    if (!await ring.HasKeysAsync(keySet))
                    {
                        await ring.GetSigningKeyAsync(keySet);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "The first signing key of the key set '{KeySet}' could not be made.", keySet);
                }
            }
        });

        return Task.CompletedTask;
    }
}
