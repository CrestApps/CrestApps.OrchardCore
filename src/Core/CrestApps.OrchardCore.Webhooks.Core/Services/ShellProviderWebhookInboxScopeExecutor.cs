using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Runs inbox work in an Orchard child shell scope, or in a new tenant scope when no shell scope is current, such as
/// after the response that accepted a delivery has completed.
/// </summary>
public sealed class ShellProviderWebhookInboxScopeExecutor : IProviderWebhookInboxScopeExecutor
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellProviderWebhookInboxScopeExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The current shell service provider.</param>
    public ShellProviderWebhookInboxScopeExecutor(IServiceProvider serviceProvider)
    {
        // Taken now, while the scope this executor came from is alive: work that outlives that scope finds it disposed.
        _shellHost = serviceProvider.GetService<IShellHost>();
        _shellSettings = serviceProvider.GetService<ShellSettings>();
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<IProviderWebhookInbox, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is not null)
        {
            await ShellScope.UsingChildScopeAsync(scope =>
                operation(scope.ServiceProvider.GetRequiredService<IProviderWebhookInbox>()));

            return;
        }

        if (_shellHost is null || _shellSettings is null)
        {
            throw new InvalidOperationException("Inbox work outside a shell scope needs the shell host and the tenant's settings.");
        }

        var scope = await _shellHost.GetScopeAsync(_shellSettings);

        await scope.UsingAsync(shellScope =>
            operation(shellScope.ServiceProvider.GetRequiredService<IProviderWebhookInbox>()));
    }
}
