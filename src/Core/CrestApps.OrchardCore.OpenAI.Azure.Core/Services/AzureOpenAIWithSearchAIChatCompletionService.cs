using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIWithSearchAIChatCompletionService : IAIChatCompletionService
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IAILinkGenerator _openAILinkGenerator;
    private readonly AzureAISearchIndexSettingsService _azureAISearchIndexSettingsService;
    private readonly IAIToolsService _toolsService;
    private readonly DefaultOpenAIOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureOpenAIWithSearchAIChatCompletionService(
        IAIDeploymentStore deploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IAILinkGenerator openAILinkGenerator,
        AzureAISearchIndexSettingsService azureAISearchIndexSettingsService,
        IAIToolsService toolService,
        IOptions<DefaultOpenAIOptions> defaultOptions,
        ILogger<AzureOpenAIChatCompletionService> logger)
    {
        _deploymentStore = deploymentStore;
        _openAILinkGenerator = openAILinkGenerator;
        _azureAISearchIndexSettingsService = azureAISearchIndexSettingsService;
        _toolsService = toolService;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _logger = logger;
    }

    public string Name { get; } = AzureWithAzureAISearchProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AIChatCompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        string deploymentName = null;

        if (_providerOptions.Providers.TryGetValue(AzureOpenAIConstants.AzureProviderName, out var entry))
        {
            var connectionName = entry.DefaultConnectionName;
            deploymentName = entry.DefaultDeploymentName;

            var deployment = await GetDeploymentAsync(context);

            if (deployment is not null)
            {
                connectionName = deployment.ConnectionName;
                deploymentName = deployment.Name;
            }

            if (!string.IsNullOrEmpty(connectionName) && entry.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                connection = connectionProperties;
            }
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile.Id);

            return AIChatCompletionResponse.Empty;
        }

        var azureMessages = new List<ChatMessage>();

        var currentPrompt = string.Empty;

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            if (message.Role == Microsoft.Extensions.AI.ChatRole.User)
            {
                azureMessages.Add(new UserChatMessage(message.Text));
                currentPrompt = message.Text;
            }
            else if (message.Role == Microsoft.Extensions.AI.ChatRole.Assistant)
            {
                azureMessages.Add(new AssistantChatMessage(message.Text));
            }
        }

        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;
        var skip = GetTotalMessagesToSkip(azureMessages.Count, pastMessageCount);

        var prompts = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemMessage(context, metadata))
        };

        prompts.AddRange(azureMessages.Skip(skip).Take(pastMessageCount));

        var azureClient = GetChatClient(connection);

        var chatClient = azureClient.GetChatClient(deploymentName);

        var chatOptions = await GetOptionsWithDataSourceAsync(context);

        try
        {
            var data = await chatClient.CompleteChatAsync(prompts, chatOptions, cancellationToken);

            if (data is null)
            {
                return AIChatCompletionResponse.Empty;
            }

            if (data.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, data.Value.ToolCalls);

                // Create a new chat option that excludes references to data sources to address the limitations in Azure OpenAI.
                data = await chatClient.CompleteChatAsync(prompts, GetOptions(context), cancellationToken);
            }

            if (data.Value.FinishReason == ChatFinishReason.Stop)
            {
                return GetResponse(data, currentPrompt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get chat completion result from Azure OpenAI.");
        }

        return AIChatCompletionResponse.Empty;
    }

    private async Task ProcessToolCallsAsync(List<ChatMessage> prompts, IEnumerable<ChatToolCall> tollCalls)
    {
        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCalls)
        {
            var function = _toolsService.GetFunction(toolCall.FunctionName);

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArguments.ToObjectFromJson<Dictionary<string, object>>();

            var result = await function.InvokeAsync(arguments);

            if (result is string str)
            {
                prompts.Add(new ToolChatMessage(toolCall.Id, str));
            }
            else
            {
                var resultJson = JsonSerializer.Serialize(result);

                prompts.Add(new ToolChatMessage(toolCall.Id, resultJson));
            }
        }
    }

    private static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }

    private static string GetSystemMessage(AIChatCompletionContext context, OpenAIChatProfileMetadata metadata)
    {
        var systemMessage = metadata.SystemMessage ?? string.Empty;

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }

    private static AzureOpenAIClient GetChatClient(AIProviderConnection connection)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient;
    }

    private async Task<ChatCompletionOptions> GetOptionsWithDataSourceAsync(AIChatCompletionContext context)
    {
        if (!context.Profile.TryGet<AzureAIChatProfileAISearchMetadata>(out var metadata))
        {
            throw new InvalidOperationException();
        }

        var chatOptions = GetOptions(context);

        var indexSettings = await _azureAISearchIndexSettingsService.GetAsync(metadata.IndexName);

        if (indexSettings == null
            || string.IsNullOrEmpty(indexSettings.IndexFullName)
            || !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return chatOptions;
        }

        var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        chatOptions.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexSettings.IndexFullName,

            Authentication = DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key),
            Strictness = metadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = metadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = "simple",
            InScope = true,
            SemanticConfiguration = "default",
            OutputContexts = DataSourceOutputContexts.Citations,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = GetBestTitleField(keyField),
                FilePathFieldName = keyField?.AzureFieldKey,
                ContentFieldSeparator = Environment.NewLine,
            }
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return chatOptions;
    }

    private ChatCompletionOptions GetOptions(AIChatCompletionContext context)
    {
        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

        var chatOptions = new ChatCompletionOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokenCount = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        if (!context.DisableTools)
        {
            foreach (var functionName in context.Profile.FunctionNames)
            {
                var function = _toolsService.GetFunction(functionName);

                if (function is null)
                {
                    continue;
                }

                chatOptions.Tools.Add(function.ToChatTool());
            }
        }

        return chatOptions;
    }

    private AIChatCompletionResponse GetResponse(ClientResult<ChatCompletion> data, string userPrompt)
    {
        var routeValues = new Dictionary<string, object>()
        {
            { "prompt", userPrompt },
        };

        var results = new List<AIChatCompletionChoice>();

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var context = data.Value.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        for (var i = 0; i < data.Value.Content.Count; i++)
        {
            var choice = data.Value.Content[i];

            if (string.IsNullOrEmpty(choice.Text))
            {
                continue;
            }

            if (context?.Citations is null || context.Citations.Count == 0)
            {
                results.Add(new AIChatCompletionChoice()
                {
                    Content = Regex.Replace(choice.Text, @"\[doc\d+\]", string.Empty),
                });

                continue;
            }

            var responseChoice = new AIChatCompletionChoice()
            {
                ContentItemIds = [],
            };

            var referenceBuilder = new StringBuilder();

            // Key is contentItemId and the index to use as template.
            var map = new Dictionary<string, int>();

            // Occasionally, templates like this [doc1][doc2] are used.
            // To prevent concatenating two numbers, a comma is added.
            var message = choice.Text.Replace("][doc", "][--reference-separator--][doc");

            for (var c = 0; c < context.Citations.Count; c++)
            {
                var citation = context.Citations[c];
                var referenceIndex = c + 1;

                var citationTemplate = $"[doc{c + 1}]";

                var hasFilePath = !string.IsNullOrEmpty(citation.FilePath);

                var needsReference = hasFilePath && choice.Text.Contains(citationTemplate);

                if (needsReference && hasFilePath)
                {
                    var referenceTitle = citation.Title;

                    if (string.IsNullOrWhiteSpace(referenceTitle))
                    {
                        referenceTitle = citationTemplate;
                    }

                    if (map.TryGetValue(citation.FilePath, out var index))
                    {
                        // Reuse existing citation reference.
                        referenceIndex = index;
                    }
                    else
                    {
                        referenceIndex = map.LastOrDefault().Value + 1;

                        responseChoice.ContentItemIds.Add(citation.FilePath);

                        // Create new citation reference.
                        map[citation.FilePath] = referenceIndex;

                        var link = _openAILinkGenerator.GetContentItemPath(citation.FilePath, routeValues);

                        if (!string.IsNullOrEmpty(link))
                        {
                            referenceBuilder.AppendLine($"{referenceIndex}. [{referenceTitle}]({link})");
                        }
                        else
                        {
                            referenceBuilder.AppendLine($"{referenceIndex}. {referenceTitle}");
                        }
                    }
                }

                message = message.Replace(citationTemplate, needsReference ? $"<sup>{referenceIndex}</sup>" : string.Empty);
            }

            // During replacements, we could end up with multiple [--reference-separator--]
            // back to back. We can replace them with a single comma.
            responseChoice.Content = Regex.Replace(message, @"(\[--reference-separator--\])+", "<sup>,</sup>")
                + Environment.NewLine + referenceBuilder.ToString();

            results.Add(responseChoice);
        }

        return new AIChatCompletionResponse
        {
            Choices = results,
        };
    }

    private async Task<AIDeployment> GetDeploymentAsync(AIChatCompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }

    private static string GetBestTitleField(AzureAISearchIndexMap keyField)
    {
        if (keyField == null || keyField.AzureFieldKey == IndexingConstants.ContentItemIdKey)
        {
            return AzureAISearchIndexManager.DisplayTextAnalyzedKey;
        }

        return null;
    }
}
