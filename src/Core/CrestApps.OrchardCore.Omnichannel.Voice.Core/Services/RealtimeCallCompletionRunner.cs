using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Finishes a live call in a child shell scope, so it no longer depends on the request the session ran inside.
/// </summary>
public sealed class RealtimeCallCompletionRunner : IRealtimeCallCompletionRunner
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeCallCompletionRunner"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, used when there is no ambient shell scope to branch from.</param>
    public RealtimeCallCompletionRunner(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task RunAsync(RealtimeCallCompletion completion)
    {
        // No ambient scope to branch from — a background task, or a test. Make one.
        if (ShellScope.Current is null)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            await scope.ServiceProvider
                .GetRequiredService<VoiceAgentConversationLoop>()
                .FinishRealtimeCallAsync(completion);

            return;
        }

        await ShellScope.UsingChildScopeAsync(scope =>
            scope.ServiceProvider
                .GetRequiredService<VoiceAgentConversationLoop>()
                .FinishRealtimeCallAsync(completion));
    }
}
