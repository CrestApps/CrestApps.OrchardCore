using CrestApps.Core.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Runs detached work on a shell scope belonging to the tenant rather than to the request.
/// </summary>
public sealed class ShellDetachedWorkExecutor : IDetachedWorkExecutor
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellDetachedWorkExecutor"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open a scope that outlives the request.</param>
    /// <param name="shellSettings">The tenant the work belongs to.</param>
    /// <param name="logger">The logger.</param>
    public ShellDetachedWorkExecutor(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<ShellDetachedWorkExecutor> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Run(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Not awaited: the caller's request is finished or abandoned, and the work must not be tied to
        // whether it lives long enough to see it through.
        _ = RunAsync(operation);
    }

    private async Task RunAsync(Func<IServiceProvider, Task> operation)
    {
        ShellScope scope;

        try
        {
            scope = await _shellHost.GetScopeAsync(_shellSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open a tenant scope for detached work.");

            return;
        }

        await scope.UsingAsync(async childScope =>
        {
            try
            {
                await operation(childScope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detached work failed.");
            }
        });
    }
}
