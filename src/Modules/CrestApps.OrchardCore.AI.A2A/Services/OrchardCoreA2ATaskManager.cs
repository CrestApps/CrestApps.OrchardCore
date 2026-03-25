using A2A;
using CrestApps.OrchardCore.AI.A2A.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.A2A.Services;

/// <summary>
/// Creates and configures an A2A <see cref="TaskManager"/> that routes incoming messages to local AI Agent profiles.
/// Uses the task-based flow (<see cref="ITaskManager.OnTaskCreated"/>) instead of <see cref="ITaskManager.OnMessageReceived"/>
/// to enable true streaming: each AI completion chunk is pushed as a <see cref="TaskArtifactUpdateEvent"/> via SSE.
/// Uses <see cref="IHttpContextAccessor"/> to resolve services per-request, avoiding captured service provider disposal issues.
/// </summary>
internal static class A2ATaskManagerFactory
{
    public static ITaskManager Create(IServiceProvider serviceProvider)
    {
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

        var taskManager = new TaskManager();

        taskManager.OnAgentCardQuery = async (agentUrl, cancellationToken) =>
        {
            var services = httpContextAccessor.HttpContext.RequestServices;
            var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profiles = await profileManager.GetAsync(AIProfileType.Agent);

            if (options.ExposeAgentsAsSkill)
            {
                return BuildSkillModeCard(agentUrl, profiles);
            }

            // Multi-agent mode: return card for the specific agent from query parameter.
            var agentName = httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();
            var targetProfile = ResolveAgentProfile(profiles, agentName);

            if (targetProfile is null)
            {
                return BuildSkillModeCard(agentUrl, profiles);
            }

            return BuildAgentCard(targetProfile, agentUrl);
        };

        // Use the task-based flow for both streaming and non-streaming.
        // When streaming, OnTaskCreated runs in a background Task.Run and pushes
        // artifact/status events through the TaskUpdateEventEnumerator.
        // When non-streaming, OnTaskCreated runs synchronously before the task is returned.
        taskManager.OnTaskCreated = (agentTask, cancellationToken) =>
            ProcessAgentTaskAsync(taskManager, httpContextAccessor, agentTask, cancellationToken);

        taskManager.OnTaskUpdated = (agentTask, cancellationToken) =>
            ProcessAgentTaskAsync(taskManager, httpContextAccessor, agentTask, cancellationToken);

        return taskManager;
    }

    private static async Task ProcessAgentTaskAsync(
        TaskManager taskManager,
        IHttpContextAccessor httpContextAccessor,
        AgentTask agentTask,
        CancellationToken cancellationToken)
    {
        var services = httpContextAccessor.HttpContext?.RequestServices;

        if (services is null)
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, "Request services are not available."),
                final: true,
                cancellationToken);

            return;
        }

        var logger = services.GetRequiredService<ILogger<TaskManager>>();
        var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;

        // Extract the user's prompt from the last message in the task history.
        var lastMessage = agentTask.History?.LastOrDefault();
        var prompt = lastMessage?.Parts?.OfType<TextPart>().FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, "No text message was provided."),
                final: true,
                cancellationToken);

            return;
        }

        var targetProfile = await ResolveTargetProfileAsync(
            services, httpContextAccessor, options, lastMessage);

        if (targetProfile is null)
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, "No agents are available to process this request."),
                final: true,
                cancellationToken);

            return;
        }

        try
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Working,
                cancellationToken: cancellationToken);

            var completionService = services.GetRequiredService<IAICompletionService>();
            var contextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
            var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

            var context = await contextBuilder.BuildAsync(targetProfile);
            context.DisableTools = true;

            var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentId: context.ChatDeploymentId)
                ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for profile '{targetProfile.Name}'.");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            var responseText = new System.Text.StringBuilder();

            await foreach (var update in completionService.CompleteStreamingAsync(
                deployment,
                messages,
                context,
                cancellationToken))
            {
                var chunk = update.Text;

                if (!string.IsNullOrEmpty(chunk))
                {
                    responseText.Append(chunk);

                    // Push each chunk as an artifact update so streaming clients receive it in real-time.
                    await taskManager.ReturnArtifactAsync(
                        agentTask.Id,
                        new Artifact
                        {
                            Parts = [new TextPart { Text = chunk }],
                        },
                        cancellationToken);
                }
            }

            var finalText = responseText.Length > 0
                ? responseText.ToString()
                : "The agent did not produce a response.";

            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Completed,
                CreateAgentMessage(agentTask.ContextId, finalText),
                final: true,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Canceled,
                final: true,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute agent '{AgentName}'.", targetProfile.Name);

            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, $"An error occurred while executing agent '{targetProfile.Name}'."),
                final: true,
                cancellationToken: CancellationToken.None);
        }
    }

    private static async Task<AIProfile> ResolveTargetProfileAsync(
        IServiceProvider services,
        IHttpContextAccessor httpContextAccessor,
        A2AHostOptions options,
        AgentMessage lastMessage)
    {
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent);

        AIProfile targetProfile = null;

        // In multi-agent mode, check query parameter first.
        if (!options.ExposeAgentsAsSkill)
        {
            var agentName = httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();

            if (!string.IsNullOrEmpty(agentName))
            {
                targetProfile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, agentName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Fall back to message metadata-based routing.
        if (targetProfile is null &&
            lastMessage?.Metadata?.TryGetValue("agentName", out var agentNameElement) == true)
        {
            var metaAgentName = agentNameElement.GetString();

            if (!string.IsNullOrEmpty(metaAgentName))
            {
                targetProfile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, metaAgentName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Fall back to the first available agent.
        return targetProfile ?? profiles?.FirstOrDefault();
    }

    internal static AgentCard BuildSkillModeCard(string agentUrl, IEnumerable<AIProfile> profiles)
    {
        var skills = new List<AgentSkill>();

        if (profiles is not null)
        {
            foreach (var profile in profiles)
            {
                skills.Add(new AgentSkill
                {
                    Id = profile.Name,
                    Name = profile.DisplayText ?? profile.Name,
                    Description = profile.Description,
                    Tags = ["agent"],
                });
            }
        }

        return new AgentCard
        {
            Name = "Orchard Core A2A Host",
            Description = "Exposes Orchard Core AI Agent profiles via the Agent-to-Agent protocol.",
            Url = agentUrl,
            Version = CrestAppsManifestConstants.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = true,
            },
            Skills = skills,
        };
    }

    internal static AgentCard BuildAgentCard(AIProfile profile, string agentUrl)
    {
        return new AgentCard
        {
            Name = profile.DisplayText ?? profile.Name,
            Description = profile.Description ?? $"AI Agent: {profile.DisplayText ?? profile.Name}",
            Url = agentUrl,
            Version = CrestAppsManifestConstants.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = true,
            },
        };
    }

    private static AIProfile ResolveAgentProfile(IEnumerable<AIProfile> profiles, string agentName)
    {
        if (string.IsNullOrEmpty(agentName) || profiles is null)
        {
            return null;
        }

        return profiles.FirstOrDefault(p =>
            string.Equals(p.Name, agentName, StringComparison.OrdinalIgnoreCase));
    }

    private static AgentMessage CreateAgentMessage(string contextId, string text)
    {
        return new AgentMessage
        {
            Role = MessageRole.Agent,
            MessageId = Guid.NewGuid().ToString(),
            ContextId = contextId,
            Parts = [new TextPart { Text = text }],
        };
    }
}
