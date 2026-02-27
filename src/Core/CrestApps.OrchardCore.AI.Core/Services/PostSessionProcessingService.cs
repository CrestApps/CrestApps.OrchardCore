using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using J2N.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Processes configured post-session tasks after a chat session is closed.
/// Analyzes the full conversation transcript using AI to produce structured results
/// such as disposition, summary, or sentiment.
/// </summary>
public sealed class PostSessionProcessingService
{
    private readonly IAIClientFactory _clientFactory;
    private readonly IAITemplateService _aiTemplateService;
    private readonly IClock _clock;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public PostSessionProcessingService(
        IAIClientFactory clientFactory,
        IAITemplateService aiTemplateService,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        ILogger<PostSessionProcessingService> logger)
    {
        _clientFactory = clientFactory;
        _aiTemplateService = aiTemplateService;
        _clock = clock;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs all configured post-session tasks against the closed session.
    /// Returns the results keyed by task name, or null if processing is not enabled.
    /// </summary>
    public async Task<Dictionary<string, PostSessionResult>> ProcessAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken = default)
    {
        var settings = profile.GetSettings<AIProfilePostSessionSettings>();

        if (!settings.EnablePostSessionProcessing || settings.PostSessionTasks.Count == 0)
        {
            return null;
        }

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                _logger.LogWarning("Unable to create a chat client for post-session processing on profile '{ProfileId}'.", profile.ItemId);
                return null;
            }

            var prompt = BuildProcessingPrompt(settings.PostSessionTasks, prompts);

            if (string.IsNullOrEmpty(prompt))
            {
                return null;
            }

            var systemPrompt = await _aiTemplateService.RenderAsync(AITemplateIds.PostSessionAnalysis);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, prompt),
            };

            var response = await chatClient.GetResponseAsync<PostSessionProcessingResponse>(messages, new ChatOptions
            {
                Temperature = 0f,
                MaxOutputTokens = 2048,
            }, null, cancellationToken);

            if (response.Result?.Tasks == null || response.Result.Tasks.Count == 0)
            {
                return null;
            }

            return ApplyResults(settings.PostSessionTasks, response.Result.Tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-session processing failed for session '{SessionId}'.", session.SessionId);
            return null;
        }
    }

    private Dictionary<string, PostSessionResult> ApplyResults(
        List<PostSessionTask> tasks,
        List<PostSessionTaskResult> results)
    {
        var now = _clock.UtcNow;
        var applied = new Dictionary<string, PostSessionResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (string.IsNullOrWhiteSpace(result.Name) || string.IsNullOrWhiteSpace(result.Value))
            {
                continue;
            }

            var task = tasks.FirstOrDefault(t =>
                string.Equals(t.Name, result.Name, StringComparison.OrdinalIgnoreCase));

            if (task == null)
            {
                continue;
            }

            // For PredefinedOptions type, validate the value(s) against the configured options.
            if (task.Type == PostSessionTaskType.PredefinedOptions && task.Options.Count > 0)
            {
                var optionValues = task.Options.Select(o => o.Value).ToList();

                if (task.AllowMultipleValues)
                {
                    // Validate each comma-separated value.
                    var selectedValues = result.Value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    var validValues = selectedValues
                        .Where(v => optionValues.Any(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)))
                        .Select(v => optionValues.First(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (validValues.Count == 0)
                    {
                        continue;
                    }

                    result.Value = string.Join(", ", validValues);
                }
                else
                {
                    var matchedOption = optionValues.FirstOrDefault(o =>
                        string.Equals(o, result.Value, StringComparison.OrdinalIgnoreCase));

                    if (matchedOption == null)
                    {
                        continue;
                    }

                    result.Value = matchedOption;
                }
            }

            applied[task.Name] = new PostSessionResult
            {
                Name = task.Name,
                Value = result.Value,
                ProcessedAtUtc = now,
            };
        }

        return applied;
    }

    private async Task<IChatClient> GetChatClientAsync(AIProfile profile)
    {
        if (!_providerOptions.Providers.TryGetValue(profile.Source, out var provider))
        {
            return null;
        }

        var connectionName = !string.IsNullOrEmpty(profile.ConnectionName)
            ? profile.ConnectionName
            : provider.DefaultConnectionName;

        if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        var deploymentName = connection.GetUtilityDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetChatDeploymentName(throwException: false);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _clientFactory.CreateChatClientAsync(profile.Source, connectionName, deploymentName);
    }

    private static string BuildProcessingPrompt(List<PostSessionTask> tasks, IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        var builder = new StringBuffer("Analyze the following completed chat conversation and produce results for the requested tasks.");

        builder.AppendLine();
        builder.Append("Tasks to process:");

        foreach (var task in tasks)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(task.Name);
            builder.Append(" (type: ");
            builder.Append(task.Type.ToString());
            builder.Append(")");

            if (!string.IsNullOrEmpty(task.Instructions))
            {
                builder.Append(": ");
                builder.Append(task.Instructions);
            }

            if (task.Type == PostSessionTaskType.PredefinedOptions && task.Options.Count > 0)
            {
                if (task.AllowMultipleValues)
                {
                    builder.Append(" [allowMultiple=true]");
                }

                builder.Append(" Options: [");

                for (var i = 0; i < task.Options.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(task.Options[i].Value);

                    if (!string.IsNullOrWhiteSpace(task.Options[i].Description))
                    {
                        builder.Append(" (");
                        builder.Append(task.Options[i].Description);
                        builder.Append(")");
                    }
                }

                builder.Append("]");
            }
        }

        // Build the conversation transcript.
        builder.AppendLine();
        builder.AppendLine();
        builder.Append("Conversation transcript:");

        foreach (var prompt in prompts)
        {
            if (prompt.IsGeneratedPrompt)
            {
                continue;
            }

            builder.AppendLine();
            builder.Append(prompt.Role == ChatRole.User ? "User: " : "Assistant: ");
            builder.Append(prompt.Content?.Trim());
        }

        return builder.ToString();
    }
}

public sealed class PostSessionProcessingResponse
{
    public List<PostSessionTaskResult> Tasks { get; set; } = [];
}

public sealed class PostSessionTaskResult
{
    public string Name { get; set; }

    public string Value { get; set; }
}
