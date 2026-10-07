using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

public sealed partial class VoiceAgentConversationLoop
{
    // Long enough for a chat model to read a few minutes of conversation; a summary that takes longer is not worth
    // holding a deferred scope open for.
    private static readonly TimeSpan _handoffSummaryTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Writes the assistant's summary of the conversation onto the activity once the handoff has been committed, so the
    /// agent who takes the call can read on the completion screen what the customer already said.
    /// </summary>
    /// <remarks>
    /// After the commit, not before the handoff: the caller is waiting for a person, and a summary is never worth
    /// keeping them waiting for. It runs in a scope of its own and reloads the activity, so it cannot collide with
    /// the handoff's own write; an agent who changes the activity in the moment between is left alone and the summary
    /// is dropped, rather than overwriting what they did.
    /// </remarks>
    /// <param name="activityId">The activity that was handed to an agent.</param>
    private static void SummarizeForTheAgentAfterCommit(string activityId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await WriteHandoffSummaryAsync(scope.ServiceProvider, activityId);
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<VoiceAgentConversationLoop>>()
                    .LogWarning(ex, "Could not summarize AI voice activity '{ActivityId}' for the agent it was handed to; the agent sees no summary.", activityId.SanitizeLogValue());
            }
        });
    }

    private static async Task WriteHandoffSummaryAsync(IServiceProvider services, string activityId)
    {
        var store = services.GetRequiredService<IOmnichannelActivityStore>();
        var activity = await store.FindByIdAsync(activityId);

        if (activity is null ||
            string.IsNullOrWhiteSpace(activity.AISessionId) ||
            !string.IsNullOrWhiteSpace(activity.HandoffSummary))
        {
            return;
        }

        var prompts = await services.GetRequiredService<IAIChatSessionPromptStore>().GetPromptsAsync(activity.AISessionId);
        var transcript = BuildHandoffTranscript(prompts);

        // Nothing was said, and a model asked to summarize nothing invents a conversation. No summary is the truth.
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        var profile = await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(activity.AIProfileId ?? string.Empty);

        if (profile is null)
        {
            return;
        }

        var systemPrompt = await services.GetRequiredService<ITemplateService>().RenderAsync(
            VoiceTemplateIds.HandoffSummary,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));

        var context = await services.GetRequiredService<IAICompletionContextBuilder>().BuildAsync(profile, builder =>
        {
            builder.SystemMessage = systemPrompt;
            builder.DisableTools = true;
        });

        // The chat slot, as the conversation's review uses: the summary is text, and the profile's voice deployment
        // cannot produce a text completion.
        var deployment = await services.GetRequiredService<IAIDeploymentManager>().ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            deploymentName: context.ChatDeploymentName);

        if (deployment is null)
        {
            return;
        }

        var client = await services.GetRequiredService<IAIClientFactory>()
            .CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience());

        using var timeout = new CancellationTokenSource(_handoffSummaryTimeout);

        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, "Call transcript:" + Environment.NewLine + transcript),
            ],
            cancellationToken: timeout.Token);

        var summary = response?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        activity.HandoffSummary = summary;
        activity.HandoffSummaryUtc = services.GetRequiredService<IClock>().UtcNow;

        await store.UpdateAsync(activity);

        try
        {
            await services.GetRequiredService<ISession>().SaveChangesAsync();
        }
        catch (ConcurrencyException)
        {
            services.GetRequiredService<ILogger<VoiceAgentConversationLoop>>().LogWarning(
                "AI voice activity '{ActivityId}' changed while its handoff summary was written; the summary was not saved.",
                activityId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// The conversation as the customer heard it, one line per turn, for the handoff summary to read.
    /// </summary>
    /// <param name="prompts">The stored turns of the assistant's session.</param>
    /// <returns>The transcript, or an empty string when nothing was said.</returns>
    internal static string BuildHandoffTranscript(IEnumerable<AIChatSessionPrompt> prompts)
    {
        var lines = (prompts ?? [])
            .Where(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content))
            .Select(prompt =>
            {
                var content = prompt.Content.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();

                return string.IsNullOrEmpty(content)
                    ? null
                    : $"{(prompt.Role == ChatRole.Assistant ? "Assistant" : "Customer")}: {content}";
            })
            .Where(line => line is not null);

        return string.Join(Environment.NewLine, lines);
    }
}
