using System.Text.Json;
using System.Text.Json.Serialization;
using A2A;
using A2A.AspNetCore;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Completions;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Services;

internal static class A2AHostExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IServiceCollection AddA2AHost(this IServiceCollection services)
    {
        services.AddSingleton<ITaskManager>(CreateTaskManager);

        return services;
    }

    public static IEndpointRouteBuilder MapA2AHost(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/agent-card.json", HandleWellKnownEndpointAsync);

        var taskManager = endpoints.ServiceProvider.GetRequiredService<ITaskManager>();
        endpoints.MapA2A(taskManager, "a2a");

        return endpoints;
    }

    private static ITaskManager CreateTaskManager(IServiceProvider serviceProvider)
    {
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var taskManager = new TaskManager();

        taskManager.OnAgentCardQuery = async (agentUrl, cancellationToken) =>
        {
            var services = httpContextAccessor.HttpContext!.RequestServices;
            var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;
            var profileManager = services.GetRequiredService<IAIProfileManager>();
            var profiles = await profileManager.GetAsync(AIProfileType.Agent);

            if (options.ExposeAgentsAsSkill)
            {
                return BuildSkillModeCard(agentUrl, profiles);
            }

            var agentName = httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();
            var targetProfile = ResolveAgentProfile(profiles, agentName);

            return targetProfile is not null
                ? BuildAgentCard(targetProfile, agentUrl)
                : BuildSkillModeCard(agentUrl, profiles);
        };

        taskManager.OnTaskCreated = (agentTask, cancellationToken) =>
            ProcessAgentTaskAsync(taskManager, httpContextAccessor, agentTask, cancellationToken);

        taskManager.OnTaskUpdated = (agentTask, cancellationToken) =>
            ProcessAgentTaskAsync(taskManager, httpContextAccessor, agentTask, cancellationToken);

        return taskManager;
    }

    private static async Task HandleWellKnownEndpointAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<A2AHostOptions>>().Value;
        var profileManager = context.RequestServices.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent);
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

        context.Response.ContentType = "application/json";

        if (options.ExposeAgentsAsSkill)
        {
            var card = BuildSkillModeCard($"{baseUrl}/a2a", profiles);
            ApplySecuritySchemes(card, options, baseUrl);
            await context.Response.WriteAsJsonAsync(card, _jsonOptions, context.RequestAborted);
        }
        else
        {
            var cards = new List<AgentCard>();

            if (profiles is not null)
            {
                foreach (var profile in profiles)
                {
                    var agentUrl = $"{baseUrl}/a2a?agent={Uri.EscapeDataString(profile.Name)}";
                    var card = BuildAgentCard(profile, agentUrl);
                    ApplySecuritySchemes(card, options, baseUrl);
                    cards.Add(card);
                }
            }

            await context.Response.WriteAsJsonAsync(cards, _jsonOptions, context.RequestAborted);
        }
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
                cancellationToken: cancellationToken);

            return;
        }

        var logger = services.GetRequiredService<ILogger<TaskManager>>();

        var lastMessage = agentTask.History?.LastOrDefault();
        var prompt = lastMessage?.Parts?.OfType<TextPart>().FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, "No text message was provided."),
                final: true,
                cancellationToken: cancellationToken);

            return;
        }

        var targetProfile = await ResolveTargetProfileAsync(
            services, httpContextAccessor, lastMessage);

        if (targetProfile is null)
        {
            await taskManager.UpdateStatusAsync(
                agentTask.Id,
                TaskState.Failed,
                CreateAgentMessage(agentTask.ContextId, "No agents are available to process this request."),
                final: true,
                cancellationToken: cancellationToken);

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

            var deployment = await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Chat, deploymentName: context.ChatDeploymentName)
                ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for profile '{targetProfile.Name}'.");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            var responseText = new System.Text.StringBuilder();

            await foreach (var update in completionService.CompleteStreamingAsync(
                deployment, messages, context, cancellationToken))
            {
                var chunk = update.Text;

                if (!string.IsNullOrEmpty(chunk))
                {
                    responseText.Append(chunk);

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
                cancellationToken: cancellationToken);
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
        AgentMessage lastMessage)
    {
        var options = services.GetRequiredService<IOptions<A2AHostOptions>>().Value;
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent);

        AIProfile targetProfile = null;

        if (!options.ExposeAgentsAsSkill)
        {
            var agentName = httpContextAccessor.HttpContext?.Request.Query["agent"].FirstOrDefault();

            if (!string.IsNullOrEmpty(agentName))
            {
                targetProfile = profiles?.FirstOrDefault(p =>
                    string.Equals(p.Name, agentName, StringComparison.OrdinalIgnoreCase));
            }
        }

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

        return targetProfile ?? profiles?.FirstOrDefault();
    }

    private static AgentCard BuildSkillModeCard(string agentUrl, IEnumerable<AIProfile> profiles)
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
            Name = "CrestApps MVC A2A Host",
            Description = "Exposes AI Agent profiles via the Agent-to-Agent protocol.",
            Url = agentUrl,
            Version = "1.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = true,
            },
            Skills = skills,
        };
    }

    private static AgentCard BuildAgentCard(AIProfile profile, string agentUrl)
    {
        return new AgentCard
        {
            Name = profile.DisplayText ?? profile.Name,
            Description = profile.Description ?? $"AI Agent: {profile.DisplayText ?? profile.Name}",
            Url = agentUrl,
            Version = "1.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = true,
            },
        };
    }

    private static void ApplySecuritySchemes(AgentCard card, A2AHostOptions options, string baseUrl)
    {
        switch (options.AuthenticationType)
        {
            case A2AHostAuthenticationType.ApiKey:
                card.SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    ["apiKey"] = new ApiKeySecurityScheme(
                        name: "Authorization",
                        keyLocation: "header",
                        description: "API key authentication. Send as 'Bearer {key}' or 'ApiKey {key}' in the Authorization header."),
                };

                card.Security =
                [
                    new Dictionary<string, string[]> { ["apiKey"] = [] },
                ];
                break;

            case A2AHostAuthenticationType.OpenId:
                card.SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    ["openId"] = new OpenIdConnectSecurityScheme(
                        openIdConnectUrl: new Uri($"{baseUrl}/.well-known/openid-configuration"),
                        description: "OpenID Connect authentication."),
                };

                card.Security =
                [
                    new Dictionary<string, string[]> { ["openId"] = [] },
                ];
                break;
        }
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
