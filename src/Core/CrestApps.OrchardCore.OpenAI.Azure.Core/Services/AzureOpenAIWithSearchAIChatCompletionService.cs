using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIWithSearchAIChatCompletionService : IOpenAIChatCompletionService
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IOpenAIDeploymentStore _deploymentStore;
    private readonly IOpenAILinkGenerator _openAILinkGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIToolsService _toolsService;
    private readonly OpenAIConnectionOptions _connectionOptions;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureOpenAIWithSearchAIChatCompletionService(
        IOpenAIDeploymentStore deploymentStore,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IOpenAILinkGenerator openAILinkGenerator,
        IServiceProvider serviceProvider,
        IAIToolsService toolService,
        ILogger<AzureOpenAIChatCompletionService> logger)
    {
        _deploymentStore = deploymentStore;
        _openAILinkGenerator = openAILinkGenerator;
        _serviceProvider = serviceProvider;
        _toolsService = toolService;
        _connectionOptions = connectionOptions.Value;
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _logger = logger;
    }

    public string Name { get; } = AzureWithAzureAISearchProfileSource.Key;

    public async Task<OpenAIChatCompletionResponse> ChatAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, OpenAIChatCompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        var deployment = await _deploymentStore.FindByIdAsync(context.Profile.DeploymentId);

        if (deployment is null)
        {
            _logger.LogWarning("Unable to chat. The profile with id '{ProfileId}' is assigned to DeploymentId '{DeploymentId}' which does not exists.", context.Profile.Id, context.Profile.DeploymentId);

            return OpenAIChatCompletionResponse.Empty;
        }

        OpenAIConnectionEntry connection = null;

        if (_connectionOptions.Connections.TryGetValue(AzureOpenAIConstants.AzureDeploymentSourceName, out var connections))
        {
            connection = connections.FirstOrDefault(x => x.Name != null && x.Name.Equals(deployment.ConnectionName, StringComparison.OrdinalIgnoreCase));
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. The DeploymentId '{DeploymentId}' belongs to a connection that does not exists (i.e., '{ConnectionName}').", context.Profile.DeploymentId, deployment.ConnectionName);

            return OpenAIChatCompletionResponse.Empty;
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

        var pastMessageCount = metadata.PastMessagesCount ?? OpenAIConstants.DefaultPastMessagesCount;
        var skip = GetTotalMessagesToSkip(azureMessages.Count, pastMessageCount);

        var prompts = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemMessage(context))
        };

        prompts.AddRange(azureMessages.Skip(skip).Take(pastMessageCount));

        var azureClient = GetChatClient(connection);

        var chatClient = azureClient.GetChatClient(deployment.Name);

        var chatOptions = await GetOptionsAsync(context, true);

        try
        {
            var data = await chatClient.CompleteChatAsync(prompts, chatOptions);

            if (data is null)
            {
                return OpenAIChatCompletionResponse.Empty;
            }

            if (data.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, data.Value.ToolCalls);

                // Create a new chat option that excludes references to data sources to address the limitations in Azure OpenAI.
                data = await chatClient.CompleteChatAsync(prompts, await GetOptionsAsync(context, false));
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

        return OpenAIChatCompletionResponse.Empty;
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

    private static string GetSystemMessage(OpenAIChatCompletionContext context)
    {
        var systemMessage = context.SystemMessage ?? string.Empty;

        if (string.IsNullOrEmpty(systemMessage) && !string.IsNullOrEmpty(context.Profile.SystemMessage))
        {
            systemMessage = context.Profile.SystemMessage;
        }

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + OpenAIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }

    private static AzureOpenAIClient GetChatClient(OpenAIConnectionEntry connection)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient;
    }

    private async Task<ChatCompletionOptions> GetOptionsAsync(OpenAIChatCompletionContext context, bool includeDataSource)
    {
        if (!context.Profile.TryGet<AzureAIChatProfileAISearchMetadata>(out var searchAIMetadata))
        {
            throw new InvalidOperationException();
        }

        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

        var chatOptions = new ChatCompletionOptions()
        {
            Temperature = metadata.Temperature ?? OpenAIConstants.DefaultTemperature,
            TopP = metadata.TopP ?? OpenAIConstants.DefaultTopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? OpenAIConstants.DefaultFrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? OpenAIConstants.DefaultPresencePenalty,
            MaxOutputTokenCount = metadata.MaxTokens ?? OpenAIConstants.DefaultMaxOutputTokens,
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

                try
                {
                    BinaryData parameters = null;

                    if (function.Metadata.Parameters != null)
                    {
                        var arguments = new AzureChatFunctionParameters()
                        {
                            Properties = [],
                        };

                        foreach (var data in function.Metadata.Parameters)
                        {
                            arguments.Properties.Add(data.Name, new AzureChatFunctionParameterArgument
                            {
                                Type = data.ParameterType.Name.ToLowerInvariant(),
                                Description = data.Description,
                                IsRequired = data.IsRequired,
                            });
                        }

                        arguments.Required = arguments.Properties.Where(x => x.Value.IsRequired).Select(x => x.Key);

                        parameters = BinaryData.FromObjectAsJson(arguments, _jsonSerializerOptions);
                    }

                    chatOptions.Tools.Add(ChatTool.CreateFunctionTool(function.Metadata.Name, function.Metadata.Description, parameters));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to add the tool '{ToolName}' to the chat options.", function.Metadata.Name);
                }
            }
        }

        var azureAISearchIndexSettingsService = _serviceProvider.GetRequiredService<AzureAISearchIndexSettingsService>();

        var indexSettings = await azureAISearchIndexSettingsService.GetAsync(searchAIMetadata.IndexName);

        if (!includeDataSource
            || indexSettings == null
            || string.IsNullOrEmpty(indexSettings.IndexFullName)
            || !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return chatOptions;
        }

        var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

        // Search against AISearch instance.
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        chatOptions.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexSettings.IndexFullName,

            Authentication = DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key),
            Strictness = searchAIMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = searchAIMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
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

    private OpenAIChatCompletionResponse GetResponse(ClientResult<ChatCompletion> data, string userPrompt)
    {
        var routeValues = new Dictionary<string, object>()
        {
            { "prompt", userPrompt },
        };

        var results = new List<OpenAIChatCompletionChoice>();

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var context = data.Value.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        for (var y = 0; y < data.Value.Content.Count; y++)
        {
            var choice = data.Value.Content[y];

            if (context?.Citations is null || context.Citations.Count == 0)
            {
                results.Add(new OpenAIChatCompletionChoice()
                {
                    Content = Regex.Replace(choice.Text ?? string.Empty, @"\[doc\d+\]", string.Empty),
                });

                continue;
            }

            var responseChoice = new OpenAIChatCompletionChoice()
            {
                ContentItemIds = [],
            };

            var referenceBuilder = new StringBuilder();

            // Key is contentItemId and the index to use as template.
            var map = new Dictionary<string, int>();

            // Sometimes, there are templates like this [doc1][doc2],
            // to avoid concatenation two numbers, we add a comma.

            var choiceMessage = (choice.Text ?? string.Empty)?.Replace("][doc", "][--reference-separator--][doc");

            for (var i = 0; i < context.Citations.Count; i++)
            {
                var citation = context.Citations[i];
                var referenceIndex = i + 1;

                var citationTemplate = $"[doc{i + 1}]";

                var needsReference = choice.Text.Contains(citationTemplate);

                // Use the reference when the id is 26 chars long (content item id).
                // Some times Azure may have records that not have content item.
                if (needsReference && !string.IsNullOrEmpty(citation.FilePath))
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

                choiceMessage = choiceMessage.Replace(citationTemplate, needsReference ? $"<sup>{referenceIndex}</sup>" : string.Empty);
            }

            // During replacements, we could end up with multiple [--reference-separator--]
            // back to back. We can replace them with a single comma.
            responseChoice.Content = Regex.Replace(choiceMessage, @"(\[--reference-separator--\])+", "<sup>,</sup>")
                + Environment.NewLine + referenceBuilder.ToString();

            results.Add(responseChoice);
        }

        return new OpenAIChatCompletionResponse
        {
            Choices = results,
        };
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
