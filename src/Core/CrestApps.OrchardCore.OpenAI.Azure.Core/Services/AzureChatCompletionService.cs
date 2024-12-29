using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureChatCompletionService : IChatCompletionService
{
    private const string _useMarkdownSyntaxSystemMessage = "- Provide a response using Markdown syntax.";

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IModelDeploymentStore _deploymentStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAIConnectionOptions _connectionOptions;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly ILogger _logger;

    public AzureChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IModelDeploymentStore deploymentStore,
        LinkGenerator linkGenerator,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IServiceProvider serviceProvider,
        HtmlEncoder htmlEncoder,
        ILogger<AzureChatCompletionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _deploymentStore = deploymentStore;
        _linkGenerator = linkGenerator;
        _serviceProvider = serviceProvider;
        _connectionOptions = connectionOptions.Value;
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _htmlEncoder = htmlEncoder;
        _logger = logger;
    }

    public string Name { get; } = AzureProfileSource.Key;

    public async Task<ChatCompletionResponse> ChatAsync(IEnumerable<ChatCompletionMessage> messages, ChatCompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        var deployment = await _deploymentStore.FindByIdAsync(context.Profile.DeploymentId);

        if (deployment is null)
        {
            _logger.LogWarning("Unable to chat. The profile with id '{ProfileId}' is assigned to DeploymentId '{DeploymentId}' which does not exists.", context.Profile.Id, context.Profile.DeploymentId);

            return ChatCompletionResponse.Empty;
        }

        OpenAIConnectionEntry connection = null;

        if (_connectionOptions.Connections.TryGetValue(AzureOpenAIConstants.AzureDeploymentSourceName, out var connections))
        {
            connection = connections.FirstOrDefault(x => x.Name != null && x.Name.Equals(deployment.ConnectionName, StringComparison.OrdinalIgnoreCase));
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. The DeploymentId '{DeploymentId}' belongs to a connection that does not exists (i.e., '{ConnectionName}').", context.Profile.DeploymentId, deployment.ConnectionName);

            return ChatCompletionResponse.Empty;
        }

        var metadata = context.Profile.As<AzureAIChatProfileMetadata>();

        var systemMessage = metadata.SystemMessage ?? string.Empty;

        if (context.UserMarkdownInResponse)
        {
            systemMessage += "\r\n" + _useMarkdownSyntaxSystemMessage;
        }

        var chatMessages = messages.Where(x => (x.Role == OpenAIConstants.Roles.User || x.Role == OpenAIConstants.Roles.Assistant) && !string.IsNullOrWhiteSpace(x.Content)).ToArray();

        var finalMessages = new[]
        {
            ChatCompletionMessage.CreateMessage(systemMessage, OpenAIConstants.Roles.System),
        };

        var request = await BuildRequestAsync(context, metadata);

        if (metadata.PastMessagesCount > 0 && chatMessages.Length > metadata.PastMessagesCount)
        {
            var skip = chatMessages.Length - metadata.PastMessagesCount;

            request.Messages = finalMessages.Concat(chatMessages.Skip(skip ?? 0).Take(metadata.PastMessagesCount.Value));
        }
        else
        {
            request.Messages = finalMessages.Concat(chatMessages);
        }

        var payload = JsonContent.Create(request, options: _jsonSerializerOptions);

        var httpClient = _httpClientFactory.CreateClient(AzureOpenAIConstants.HttpClientName);

        httpClient.BaseAddress = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", connection.GetApiKey());
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

        var response = await httpClient.PostAsync($"openai/deployments/{deployment.Name}/chat/completions?api-version=2024-05-01-preview", payload);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("Unable to create chat using Azure REST API. Content: {Content}", content);

            return ChatCompletionResponse.Empty;
        }

        var data = await response.Content.ReadFromJsonAsync<AzureCompletionResponse>();

        return GetResponse(data, isContentItemDocument: false, request.Messages.LastOrDefault(x => x.Role == OpenAIConstants.Roles.User)?.Content);
    }

    private async Task<AzureCompletionRequest> BuildRequestAsync(ChatCompletionContext context, AzureAIChatProfileMetadata metadata)
    {
        var request = new AzureCompletionRequest()
        {
            Temperature = metadata.Temperature,
            TopP = metadata.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty,
            MaxTokens = metadata.MaxTokens,
        };

        if (context.Profile.Source == AzureWithAzureAISearchProfileSource.Key &&
            context.Profile.TryGet<AzureAIChatProfileAISearchMetadata>(out var searchAIMetadata))
        {
            // Search against AISearch instance.
            var dataSource = new CompletionDataSource()
            {
                Type = "azure_search",
                Parameters = [],
            };

            var azureAISearchIndexManager = _serviceProvider.GetRequiredService<AzureAISearchIndexManager>();
            var azureAISearchIndexSettingsService = _serviceProvider.GetRequiredService<AzureAISearchIndexSettingsService>();

            var indexSettings = await azureAISearchIndexSettingsService.GetAsync(searchAIMetadata.IndexName);
            var fullIndexName = azureAISearchIndexManager.GetFullIndexName(searchAIMetadata.IndexName);

            var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

            dataSource.Parameters["endpoint"] = _azureAISearchDefaultOptions.Endpoint;
            dataSource.Parameters["index_name"] = fullIndexName;
            dataSource.Parameters["semantic_configuration"] = "default";
            dataSource.Parameters["query_type"] = "simple";
            dataSource.Parameters["fields_mapping"] = GetFieldMapping(keyField);
            dataSource.Parameters["in_scope"] = true;
            dataSource.Parameters["role_information"] = metadata.SystemMessage;
            dataSource.Parameters["filter"] = null;
            dataSource.Parameters["strictness"] = searchAIMetadata.Strictness;
            dataSource.Parameters["top_n_documents"] = searchAIMetadata.TopNDocuments;

            if (_azureAISearchDefaultOptions.Credential is AzureKeyCredential keyCredential)
            {
                dataSource.Parameters["authentication"] = new JsonObject()
                {
                    {"type","api_key"},
                    {"key", keyCredential.Key},
                };
            }

            request.DataSources =
            [
                dataSource,
            ];
        }

        return request;
    }

    private ChatCompletionResponse GetResponse(AzureCompletionResponse data, bool isContentItemDocument, string userPrompt)
    {
        var routeValues = new RouteValueDictionary()
        {
            { "Area", "OrchardCore.Contents" },
            { "Controller", "Item" },
            { "Action", "Display" },
            { "tm_utility", "OpenAIChat" },
            { "tm_query", userPrompt },
        };

        if (Uri.TryCreate(_httpContextAccessor.HttpContext.Request.Headers.Referer, UriKind.Absolute, out var refererUri))
        {
            if (!string.Equals(refererUri.Host.TrimEnd('/'), _httpContextAccessor.HttpContext.Request.Host.ToString().TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                routeValues["tm_source"] = refererUri.Host;
            }
        }

        var results = new List<ChatCompletionChoice>();

        foreach (var choice in data.Choices)
        {
            if (choice.Message?.Context?.Citations == null || choice.Message.Context.Citations.Length == 0 || !isContentItemDocument)
            {
                results.Add(new ChatCompletionChoice()
                {
                    Message = choice.Message?.Content ?? string.Empty,
                });

                continue;
            }

            var responseChoice = new ChatCompletionChoice()
            {
                ContentItemIds = [],
            };

            var referenceBuilder = new StringBuilder();

            // Key is contentItemId and the index to use as template.
            var map = new Dictionary<string, int>();

            // Sometimes, there are templates like this [doc1][doc2],
            // to avoid concatenation two numbers, we add a comma.
            var choiceMessage = (choice.Message?.Content ?? string.Empty)?.Replace("][doc", "][--reference-separator--][doc");

            for (var i = 0; i < choice.Message.Context.Citations.Length; i++)
            {
                var citation = choice.Message.Context.Citations[i];
                var referenceIndex = i + 1;

                var citationTemplate = $"[doc{i + 1}]";

                var needsReference = choice.Message.Content.Contains(citationTemplate);

                // Use the reference when the id is 26 chars long (content item id).
                // Some times Azure may have records that not have content item.
                if (citation.FilePath != null && needsReference)
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

                        routeValues["contentItemId"] = citation.FilePath;

                        var link = _linkGenerator.GetPathByRouteValues(_httpContextAccessor.HttpContext, routeName: null, values: routeValues);

                        if (!string.IsNullOrEmpty(link))
                        {
                            // TODO, convert the links to Markdown list.
                            referenceBuilder.Append(CultureInfo.InvariantCulture, $" {referenceIndex}. <a href=\"{link}\" target=\"_blank\">{_htmlEncoder.Encode(referenceTitle)}</a> \r\n");
                        }
                    }
                }

                choiceMessage = choiceMessage.Replace(citationTemplate, needsReference ? $"<sup>{referenceIndex}</sup>" : string.Empty);
            }

            // During replacements, we could end up with multiple [--reference-separator--]
            // back to back. We can replace them with a single comma.
            responseChoice.Message = Regex.Replace(choiceMessage, @"(\[--reference-separator--\])+", "<sup>,</sup>")
                + "\r\n" + referenceBuilder.ToString();

            results.Add(responseChoice);
        }

        return new ChatCompletionResponse
        {
            Choices = results,
        };
    }

    private static JsonObject GetFieldMapping(AzureAISearchIndexMap keyField)
    {
        var mapping = new JsonObject()
        {
            { "content_fields_separator", "\n" },
            { "title_field", GetBestTitleField(keyField) },
            { "filepath_field", keyField?.AzureFieldKey },
            { "url_field", null },
        };

        return mapping;
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
