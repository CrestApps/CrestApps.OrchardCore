using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// Runs work once the response has been sent, in a fresh tenant shell scope.
/// </summary>
/// <remarks>
/// A provider webhook is answered as soon as its delivery is stored durably, because a slow answer makes the provider
/// send it again. Processing then starts at once rather than waiting for the next background pass. The work is not a
/// discarded task: the server awaits it after the response completes, and the work is only ever a latency optimization
/// over a durable store that a background task also drains, so nothing is lost when the process stops first.
/// </remarks>
public static class AfterResponseWork
{
    /// <summary>
    /// Schedules work to run in a fresh shell scope after the response to the current request has completed.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="shellHost">The shell host that creates the scope.</param>
    /// <param name="shellSettings">The settings of the tenant the work runs for.</param>
    /// <param name="work">The work to run.</param>
    /// <param name="logger">The logger that records a failure of the work.</param>
    public static void Schedule(
        HttpContext context,
        IShellHost shellHost,
        ShellSettings shellSettings,
        Func<ShellScope, Task> work,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shellHost);
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(logger);

        context.Response.OnCompleted(() => RunAsync(shellHost, shellSettings, work, logger));
    }

    /// <summary>
    /// Runs the work in a fresh shell scope and records a failure instead of letting it escape.
    /// </summary>
    /// <param name="shellHost">The shell host that creates the scope.</param>
    /// <param name="shellSettings">The settings of the tenant the work runs for.</param>
    /// <param name="work">The work to run.</param>
    /// <param name="logger">The logger that records a failure of the work.</param>
    public static async Task RunAsync(
        IShellHost shellHost,
        ShellSettings shellSettings,
        Func<ShellScope, Task> work,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(shellHost);
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var scope = await shellHost.GetScopeAsync(shellSettings);

            await scope.UsingAsync(work);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Work scheduled to run after the response failed. Any durable record it was processing is retried by its background task.");
        }
    }
}
