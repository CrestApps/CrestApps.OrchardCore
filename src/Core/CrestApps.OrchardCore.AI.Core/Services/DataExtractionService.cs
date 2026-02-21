using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DataExtractionService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAIClientFactory _clientFactory;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public DataExtractionService(
        IAIClientFactory clientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<DataExtractionService> logger)
    {
        _clientFactory = clientFactory;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public static bool ShouldExtract(AIProfileDataExtractionSettings settings, int promptCount)
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

    public static List<DataExtractionEntry> GetFieldsToExtract(AIProfileDataExtractionSettings settings, AIChatSession session)
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

    public async Task<List<ExtractionResult>> ExtractAsync(
        AIProfile profile,
        AIChatSession session,
        List<DataExtractionEntry> fieldsToExtract,
        string lastAssistantMessage,
        string latestUserMessage,
        CancellationToken cancellationToken = default)
    {
        if (fieldsToExtract.Count == 0)
        {
            return [];
        }

        var prompt = BuildExtractionPrompt(fieldsToExtract, session.ExtractedData, lastAssistantMessage, latestUserMessage);

        try
        {
            var chatClient = await GetChatClientAsync(profile);

            if (chatClient == null)
            {
                _logger.LogWarning("Unable to create a chat client for data extraction on profile '{ProfileId}'.", profile.ItemId);
                return [];
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, GetSystemPrompt()),
                new(ChatRole.User, prompt),
            };

            var response = await chatClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0f,
                MaxOutputTokens = 1024,
            }, cancellationToken);

            if (response?.Messages == null || response.Messages.Count == 0)
            {
                return [];
            }

            var responseText = response.Messages[response.Messages.Count - 1].Text?.Trim();

            if (string.IsNullOrEmpty(responseText))
            {
                return [];
            }

            return ParseExtractionResponse(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data extraction failed for session '{SessionId}'.", session.SessionId);
            return [];
        }
    }

    public static ExtractionChangeSet ApplyExtraction(
        AIChatSession session,
        AIProfileDataExtractionSettings settings,
        List<ExtractionResult> results,
        DateTime utcNow)
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
                foreach (var value in result.Values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (!state.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        state.Values.Add(value);
                        state.LastExtractedUtc = utcNow;
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
                    state.LastExtractedUtc = utcNow;
                    changeSet.NewFields.Add(new ExtractedFieldChange(entry.Name, value, false));
                }
                else if (entry.IsUpdatable)
                {
                    state.Values[0] = value;
                    state.LastExtractedUtc = utcNow;
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

        var deploymentName = connection.GetDefaultUtilityDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = connection.GetDefaultDeploymentName(throwException: false);
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
            You are a data extraction assistant. Your job is to extract specific fields from the user's latest message.
            Rules:
            - Only extract from the latest user message.
            - Use the last assistant message only for context interpretation.
            - Do not hallucinate values. Only extract what is explicitly stated.
            - Return strict JSON only. No explanations, no markdown.
            - Only return fields that were requested.
            - Return an empty fields array if nothing is found.
            - Response format:
            {"fields":[{"name":"fieldName","values":["value1"],"confidence":0.95}]}
            """;
    }

    private static string BuildExtractionPrompt(
        List<DataExtractionEntry> fieldsToExtract,
        Dictionary<string, ExtractedFieldState> currentState,
        string lastAssistantMessage,
        string latestUserMessage)
    {
        var parts = new List<string>
        {
            "Extract the following fields from the user's latest message.",
            "",
            "Fields to extract:",
        };

        foreach (var field in fieldsToExtract)
        {
            var desc = string.IsNullOrEmpty(field.Description)
                ? field.Name
                : field.Description;

            parts.Add($"- {field.Name}: {desc} (multiple: {field.AllowMultipleValues.ToString().ToLowerInvariant()})");
        }

        if (currentState.Count > 0)
        {
            parts.Add("");
            parts.Add("Current extracted state:");

            foreach (var (key, state) in currentState)
            {
                if (state.Values.Count > 0)
                {
                    parts.Add($"- {key}: [{string.Join(", ", state.Values)}]");
                }
            }
        }

        if (!string.IsNullOrEmpty(lastAssistantMessage))
        {
            parts.Add("");
            parts.Add("Last assistant message:");
            parts.Add(lastAssistantMessage);
        }

        parts.Add("");
        parts.Add("Latest user message:");
        parts.Add(latestUserMessage);

        return string.Join("\n", parts);
    }

    private static List<ExtractionResult> ParseExtractionResponse(string responseText)
    {
        // Strip markdown code fences if present.
        if (responseText.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = responseText.IndexOf('\n');

            if (firstNewline >= 0)
            {
                responseText = responseText[(firstNewline + 1)..];
            }

            if (responseText.EndsWith("```", StringComparison.Ordinal))
            {
                responseText = responseText[..^3];
            }

            responseText = responseText.Trim();
        }

        var parsed = JsonSerializer.Deserialize<ExtractionResponse>(responseText, _jsonOptions);

        return parsed?.Fields ?? [];
    }
}

public sealed class ExtractionResponse
{
    public List<ExtractionResult> Fields { get; set; } = [];
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
}

public sealed record ExtractedFieldChange(string FieldName, string Value, bool IsMultiple);
