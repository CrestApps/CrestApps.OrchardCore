using System.Runtime.CompilerServices;
using A2A;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// Routes incoming A2A messages to local AI Agent profiles.
/// </summary>
internal sealed class OrchardCoreA2ARequestHandler : IA2ARequestHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreA2ARequestHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    public OrchardCoreA2ARequestHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var services = GetRequestServices();
        var context = await CreateExecutionContextAsync(services, request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            return new SendMessageResponse
            {
                Message = CreateAgentMessage(context.ContextId, context.ErrorMessage),
            };
        }

        try
        {
            var completionService = services.GetRequiredService<IAICompletionService>();
            var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
            var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

            var completionContext = await completionContextBuilder.BuildAsync(context.Profile, cancellationToken: cancellationToken);
            completionContext.DisableTools = true;

            var deployment = await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentPurpose.Chat,
                deploymentName: completionContext.ChatDeploymentName,
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for profile '{context.Profile.Name}'.");

            var completion = await completionService.CompleteAsync(
                deployment,
                [new ChatMessage(ChatRole.User, context.Prompt)],
                completionContext,
                cancellationToken);

            var responseText = completion.Messages.FirstOrDefault()?.Text;

            return new SendMessageResponse
            {
                Message = CreateAgentMessage(
                    context.ContextId,
                    !string.IsNullOrEmpty(responseText) ? responseText : "The agent did not produce a response."),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logger = services.GetRequiredService<ILogger<OrchardCoreA2ARequestHandler>>();
            logger.LogError(ex, "Failed to execute agent '{AgentName}'.", context.Profile?.Name);

            return new SendMessageResponse
            {
                Message = CreateAgentMessage(context.ContextId, $"An error occurred while executing agent '{context.Profile?.Name}'."),
            };
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamResponse> SendStreamingMessageAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var services = GetRequestServices();
        var context = await CreateExecutionContextAsync(services, request, cancellationToken);
        var taskId = request.Message?.TaskId ?? Guid.NewGuid().ToString();

        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
        {
            yield return CreateStatusUpdate(taskId, context.ContextId, TaskState.Failed, _clock.UtcNow, context.ErrorMessage);
            yield break;
        }

        yield return CreateStatusUpdate(taskId, context.ContextId, TaskState.Working, _clock.UtcNow);

        var responseText = new System.Text.StringBuilder();

        var completionService = services.GetRequiredService<IAICompletionService>();
        var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();

        var completionContext = await completionContextBuilder.BuildAsync(context.Profile, cancellationToken: cancellationToken);
        completionContext.DisableTools = true;

        var deployment = await deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentPurpose.Chat,
            deploymentName: completionContext.ChatDeploymentName,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for profile '{context.Profile.Name}'.");

        await foreach (var update in completionService.CompleteStreamingAsync(
            deployment,
            [new ChatMessage(ChatRole.User, context.Prompt)],
            completionContext,
            cancellationToken))
        {
            var chunk = update.Text;

            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            responseText.Append(chunk);

            yield return new StreamResponse
            {
                ArtifactUpdate = new TaskArtifactUpdateEvent
                {
                    TaskId = taskId,
                    ContextId = context.ContextId,
                    Artifact = new Artifact
                    {
                        Parts = [Part.FromText(chunk)],
                    },
                },
            };
        }

        var finalText = responseText.Length > 0
            ? responseText.ToString()
            : "The agent did not produce a response.";

        yield return CreateStatusUpdate(taskId, context.ContextId, TaskState.Completed, _clock.UtcNow, finalText);
    }

    /// <inheritdoc/>
    public Task<AgentTask> GetTaskAsync(GetTaskRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Task retrieval is not supported by this A2A host.", A2AErrorCode.UnsupportedOperation);

    /// <inheritdoc/>
    public Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new ListTasksResponse
        {
            Tasks = [],
        });

    /// <inheritdoc/>
    public Task<AgentTask> CancelTaskAsync(CancelTaskRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Task cancellation is not supported by this A2A host.", A2AErrorCode.UnsupportedOperation);

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamResponse> SubscribeToTaskAsync(SubscribeToTaskRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Task subscription is not supported by this A2A host.", A2AErrorCode.UnsupportedOperation);

    /// <inheritdoc/>
    public Task<TaskPushNotificationConfig> CreateTaskPushNotificationConfigAsync(CreateTaskPushNotificationConfigRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Push notifications are not supported by this A2A host.", A2AErrorCode.PushNotificationNotSupported);

    /// <inheritdoc/>
    public Task<TaskPushNotificationConfig> GetTaskPushNotificationConfigAsync(GetTaskPushNotificationConfigRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Push notifications are not supported by this A2A host.", A2AErrorCode.PushNotificationNotSupported);

    /// <inheritdoc/>
    public Task<ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfigAsync(ListTaskPushNotificationConfigRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Push notifications are not supported by this A2A host.", A2AErrorCode.PushNotificationNotSupported);

    /// <inheritdoc/>
    public Task DeleteTaskPushNotificationConfigAsync(DeleteTaskPushNotificationConfigRequest request, CancellationToken cancellationToken)
        => throw new A2AException("Push notifications are not supported by this A2A host.", A2AErrorCode.PushNotificationNotSupported);

    /// <inheritdoc/>
    public async Task<AgentCard> GetExtendedAgentCardAsync(GetExtendedAgentCardRequest request, CancellationToken cancellationToken)
    {
        var services = GetRequestServices();
        var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent, cancellationToken);
        var agentUrl = BuildAgentUrl();

        if (options.ExposeAgentsAsSkill)
        {
            return BuildSkillModeCard(agentUrl, profiles);
        }

        var agentName = _httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();
        var targetProfile = ResolveAgentProfile(profiles, agentName);

        return targetProfile is not null
            ? BuildAgentCard(targetProfile, agentUrl)
            : BuildSkillModeCard(agentUrl, profiles);
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
            SupportedInterfaces = [CreateAgentInterface(agentUrl)],
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
            SupportedInterfaces = [CreateAgentInterface(agentUrl)],
            Version = CrestAppsManifestConstants.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = true,
            },
        };
    }

    private static AgentInterface CreateAgentInterface(string agentUrl)
    {
        return new AgentInterface
        {
            Url = agentUrl,
            ProtocolBinding = ProtocolBindingNames.JsonRpc,
        };
    }

    private async Task<A2AExecutionContext> CreateExecutionContextAsync(
        IServiceProvider services,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message;
        var contextId = message?.ContextId ?? Guid.NewGuid().ToString();
        var prompt = message?.Parts?.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part.Text))?.Text;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new A2AExecutionContext(contextId, null, null, "No text message was provided.");
        }

        var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;
        var targetProfile = await ResolveTargetProfileAsync(services, options, message, cancellationToken);

        if (targetProfile is null)
        {
            return new A2AExecutionContext(contextId, prompt, null, "No agents are available to process this request.");
        }

        return new A2AExecutionContext(contextId, prompt, targetProfile, null);
    }

    private async Task<AIProfile> ResolveTargetProfileAsync(
        IServiceProvider services,
        A2AHostOptions options,
        Message message,
        CancellationToken cancellationToken)
    {
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent, cancellationToken);

        AIProfile targetProfile = null;

        if (!options.ExposeAgentsAsSkill)
        {
            var agentName = _httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();

            if (!string.IsNullOrEmpty(agentName))
            {
                targetProfile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, agentName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (targetProfile is null &&
            message?.Metadata?.TryGetValue("agentName", out var agentNameElement) == true)
        {
            var metaAgentName = agentNameElement.GetString();

            if (!string.IsNullOrEmpty(metaAgentName))
            {
                targetProfile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, metaAgentName, StringComparison.OrdinalIgnoreCase));
            }
        }

        return targetProfile ?? profiles?.FirstOrDefault();
    }

    private IServiceProvider GetRequestServices()
    {
        return _httpContextAccessor.HttpContext?.RequestServices
            ?? throw new InvalidOperationException("Request services are not available.");
    }

    private string BuildAgentUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is null)
        {
            return "/a2a";
        }

        return $"{request.Scheme}://{request.Host}/a2a";
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

    private static StreamResponse CreateStatusUpdate(string taskId, string contextId, TaskState state, DateTime utcNow, string message = null)
    {
        return new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = taskId,
                ContextId = contextId,
                Status = new global::A2A.TaskStatus
                {
                    State = state,
                    Message = !string.IsNullOrEmpty(message)
                        ? CreateAgentMessage(contextId, message)
                        : null,
                    Timestamp = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)),
                },
            },
        };
    }

    private static Message CreateAgentMessage(string contextId, string text)
    {
        return new Message
        {
            Role = Role.Agent,
            MessageId = Guid.NewGuid().ToString(),
            ContextId = contextId,
            Parts = [Part.FromText(text)],
        };
    }

    private sealed record A2AExecutionContext(
        string ContextId,
        string Prompt,
        AIProfile Profile,
        string ErrorMessage);
}
