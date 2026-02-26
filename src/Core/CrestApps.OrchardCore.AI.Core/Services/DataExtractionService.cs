using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core;
using J2N.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DataExtractionService
{
    private readonly IAIClientFactory _clientFactory;
    private readonly IClock _clock;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public DataExtractionService(
        IAIClientFactory clientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        ILogger<DataExtractionService> logger)
    {
        _clientFactory = clientFactory;
        _clock = clock;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full extraction pipeline: checks whether extraction should run,
    /// determines which fields to extract, calls the AI model, and applies results
    /// to the session. Returns the change set (may be empty).
    /// </summary>
    public async Task<ExtractionChangeSet> ProcessAsync(
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken = default)
    {
        var settings = profile.GetSettings<AIProfileDataExtractionSettings>();
        var promptCount = session.Prompts.Count(p => p.Role == ChatRole.User);

        if (!ShouldExtract(settings, promptCount))
        {
            return null;
        }

        var fieldsToExtract = GetFieldsToExtract(settings, session);

        if (fieldsToExtract.Count == 0)
        {
            return null;
        }

        var (results, sessionEnded) = await ExtractAsync(profile, session, fieldsToExtract, cancellationToken);

        var changeSet = ApplyExtraction(session, settings, results);
        changeSet.SessionEnded = sessionEnded;

        return changeSet;
    }

    private static bool ShouldExtract(AIProfileDataExtractionSettings settings, int promptCount)
    {
        if (!settings.EnableDataExtraction)
        {
            return false;
        }

        if (settings.DataExtractionEntries.Count == 0)
        {
            return false;
        }

        if (settings.ExtractionCheckInterval < 1)
        {
            return false;
        }

        return promptCount % settings.ExtractionCheckInterval == 0;
    }

    private static List<DataExtractionEntry> GetFieldsToExtract(AIProfileDataExtractionSettings settings, AIChatSession session)
    {
        var fieldsToExtract = new List<DataExtractionEntry>();

        foreach (var entry in settings.DataExtractionEntries)
        {
            if (entry.AllowMultipleValues)
            {
                fieldsToExtract.Add(entry);
                continue;
            }

            if (!session.ExtractedData.TryGetValue(entry.Name, out var state) || state.Values.Count == 0)
            {
                fieldsToExtract.Add(entry);
                continue;
            }

            if (entry.IsUpdatable)
            {
                fieldsToExtract.Add(entry);
            }
        }

        return fieldsToExtract;
    }

    private async Task<(List<ExtractionResult> Results, bool SessionEnded)> ExtractAsync(
        AIProfile profile,
        AIChatSession session,
        List<DataExtractionEntry> fieldsToExtract,
        CancellationToken cancellationToken = default)
    {
        if (fieldsToExtract.Count == 0)
        {
            return ([], false);
        }

        var prompt = BuildExtractionPrompt(fieldsToExtract, session);

        if (string.IsNullOrEmpty(prompt))
        {
            return ([], false);
        }

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                _logger.LogWarning("Unable to create a chat client for data extraction on profile '{ProfileId}'.", profile.ItemId);
                return ([], false);
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, GetSystemPrompt()),
                new(ChatRole.User, prompt),
            };

            var response = await chatClient.GetResponseAsync<ExtractionResponse>(messages, new ChatOptions
            {
                Temperature = 0f,
                MaxOutputTokens = 1024,
            }, null, cancellationToken);

            if (response.Result is null || response.Result is null || response.Result.Fields.Count == 0)
            {
                return ([], false);
            }

            return (response.Result.Fields, response.Result.SessionEnded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data extraction failed for session '{SessionId}'.", session.SessionId);
            return ([], false);
        }
    }

    private ExtractionChangeSet ApplyExtraction(
        AIChatSession session,
        AIProfileDataExtractionSettings settings,
        List<ExtractionResult> results)
    {
        var changeSet = new ExtractionChangeSet();

        foreach (var result in results)
        {
            if (result.Values == null || result.Values.Count == 0)
            {
                continue;
            }

            var entry = settings.DataExtractionEntries.FirstOrDefault(e =>
                string.Equals(e.Name, result.Name, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                continue;
            }

            if (!session.ExtractedData.TryGetValue(entry.Name, out var state))
            {
                state = new ExtractedFieldState();
                session.ExtractedData[entry.Name] = state;
            }

            if (entry.AllowMultipleValues)
            {
                var now = _clock.UtcNow;

                foreach (var value in result.Values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (!state.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        state.Values.Add(value);
                        state.LastExtractedUtc = now;
                        changeSet.NewFields.Add(new ExtractedFieldChange(entry.Name, value, entry.AllowMultipleValues));
                    }
                }
            }
            else
            {
                var value = result.Values[0];

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (state.Values.Count == 0)
                {
                    state.Values.Add(value);
                    state.LastExtractedUtc = _clock.UtcNow;
                    changeSet.NewFields.Add(new ExtractedFieldChange(entry.Name, value, false));
                }
                else if (entry.IsUpdatable)
                {
                    state.Values[0] = value;
                    state.LastExtractedUtc = _clock.UtcNow;
                    changeSet.NewFields.Add(new ExtractedFieldChange(entry.Name, value, false));
                }
            }
        }

        return changeSet;
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

    private static string GetSystemPrompt()
    {
        return
            """
            You are a data extraction assistant. Your job is to extract specific fields from the user's latest message and detect whether the conversation has naturally ended.

            [Rules]
            1. Only extract from the latest user message.
            2. Use the last assistant message only for context interpretation.
            3. Do not hallucinate values. Only extract what is explicitly stated.
            4. Clean and normalize extracted values: strip trailing or leading punctuation (e.g., "!", "@", ".", ",") that is clearly not part of the value. For example, "Mike@" should be extracted as "Mike", and "mike@checkboxsigns.com!" should be extracted as "mike@checkboxsigns.com".
            5. Use context to infer the correct value type. For example, if the field is "email", recognize email-like patterns even if surrounded by stray characters.
            6. Return valid JSON only. Do NOT wrap the response in markdown code fences (```). No explanations, no comments.
            7. Only return fields that were requested.
            8. Return an empty fields array if nothing is found.
            9. Set "sessionEnded" to true if the user's message indicates a natural farewell or conversation ending (e.g., "Thank you, bye!", "That's all I needed", "Have a great day!", "Goodbye"). Otherwise, set it to false.

            [Output Format]
            {"fields":[{"name":"fieldName","values":["value1"],"confidence":0.95}],"sessionEnded":false}
            """;
    }

    private static string BuildExtractionPrompt(List<DataExtractionEntry> fieldsToExtract, AIChatSession session)
    {
        var builder = new StringBuffer("Extract the following fields from the user's latest message.");

        builder.AppendLine("Fields to extract:");

        foreach (var field in fieldsToExtract)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(field.Name);
            builder.Append(": ");

            if (!string.IsNullOrEmpty(field.Description))
            {
                builder.Append(field.Description);
                builder.Append(" ");
            }

            builder.Append("(multiple: ");
            builder.Append(field.AllowMultipleValues.ToString().ToLowerInvariant());
            builder.Append(")");
        }

        if (session.ExtractedData?.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Current extracted state:");

            foreach (var (key, state) in session.ExtractedData)
            {
                if (state.Values.Count == 0)
                {
                    continue;
                }

                builder.Append("- ");
                builder.Append(key);
                builder.Append(": [");

                foreach (var value in state.Values)
                {
                    builder.Append(value);
                    builder.Append(", ");
                }

                builder.Append("]");
            }
        }

        // Find the last user message and the assistant message immediately before it.
        string lastUserMessage = null;
        string lastAssistantMessage = null;

        for (var i = session.Prompts.Count - 1; i >= 0; i--)
        {
            if (lastUserMessage is null && session.Prompts[i].Role == ChatRole.User)
            {
                lastUserMessage = session.Prompts[i].Content?.Trim();

                // Look for the assistant message immediately before this user message.
                for (var j = i - 1; j >= 0; j--)
                {
                    if (session.Prompts[j].Role == ChatRole.Assistant)
                    {
                        lastAssistantMessage = session.Prompts[j].Content?.Trim();
                        break;
                    }
                }

                break;
            }
        }

        if (string.IsNullOrEmpty(lastUserMessage))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(lastAssistantMessage))
        {
            builder.AppendLine();
            builder.Append("Last assistant message: ");
            builder.Append(lastAssistantMessage);
        }

        builder.AppendLine();
        builder.Append("Latest user message: ");
        builder.Append(lastUserMessage);

        return builder.ToString();
    }

    private static (List<ExtractionResult> Results, bool SessionEnded) ParseExtractionResponse(string responseText)
    {
        var parsed = JsonSerializer.Deserialize<ExtractionResponse>(responseText, JSOptions.CaseInsensitive);

        return (parsed?.Fields ?? [], parsed?.SessionEnded ?? false);
    }
}

public sealed class ExtractionResponse
{
    public List<ExtractionResult> Fields { get; set; } = [];

    public bool SessionEnded { get; set; }
}

public sealed class ExtractionResult
{
    public string Name { get; set; }

    public List<string> Values { get; set; } = [];

    public double Confidence { get; set; }
}

public sealed class ExtractionChangeSet
{
    public List<ExtractedFieldChange> NewFields { get; set; } = [];

    public bool SessionEnded { get; set; }
}

public sealed record ExtractedFieldChange(string FieldName, string Value, bool IsMultiple);
