using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;
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

public sealed class AzureChatCompletionService : IOpenAIChatCompletionService
{
    private const string _useMarkdownSyntaxSystemMessage = "- Provide a response using Markdown syntax.";

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static AzureChatCompletionService()
    {
        _jsonSerializerOptions.Converters.Add(OpenAIChatFunctionPropertyConverter.Instance);
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOpenAIDeploymentStore _deploymentStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAIConnectionOptions _connectionOptions;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IOpenAIFunctionService _openAIFunctionService;
    private readonly ILogger _logger;

    public AzureChatCompletionService(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IOpenAIDeploymentStore deploymentStore,
        LinkGenerator linkGenerator,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IServiceProvider serviceProvider,
        HtmlEncoder htmlEncoder,
        IOpenAIFunctionService openAIFunctionService,
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
        _openAIFunctionService = openAIFunctionService;
        _logger = logger;
    }

    public string Name { get; } = AzureProfileSource.Key;

    public async Task<OpenAIChatCompletionResponse> ChatAsync(IEnumerable<OpenAIChatCompletionMessage> messages, OpenAIChatCompletionContext context)
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

        var metadata = context.Profile.As<OpenAIChatProfileMetadata>();

        var systemMessage = GetSystemMessage(context);

        var (request, includeContentItemCitations) = await BuildRequestAsync(context, metadata, context.Profile.FunctionNames, systemMessage);

        var chatMessages = messages.Where(x => (x.Role == OpenAIConstants.Roles.User || x.Role == OpenAIConstants.Roles.Assistant) && !string.IsNullOrWhiteSpace(x.Content)).ToArray();

        var finalMessages = new[]
        {
            OpenAIChatCompletionMessage.CreateMessage(systemMessage, OpenAIConstants.Roles.System),
        };

        var pastMessage = metadata.PastMessagesCount ?? OpenAIConstants.DefaultPastMessagesCount;

        if (pastMessage > 0 && chatMessages.Length > pastMessage)
        {
            var skip = chatMessages.Length - pastMessage;

            request.Messages = finalMessages.Concat(chatMessages.Skip(skip).Take(pastMessage));
        }
        else
        {
            request.Messages = finalMessages.Concat(chatMessages);
        }

        var httpClient = GetHttpClient(connection);

        var data = await GetResponseDataAsync(httpClient, request, deployment.Name);

        if (data is null)
        {
            return OpenAIChatCompletionResponse.Empty;
        }

        return GetResponse(
            data,
            includeContentItemCitations,
            request.Messages.LastOrDefault(x => x.Role == OpenAIConstants.Roles.User)?.Content);
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
            systemMessage += "\r\n" + _useMarkdownSyntaxSystemMessage;
        }

        return systemMessage;
    }

    private HttpClient GetHttpClient(OpenAIConnectionEntry connection)
    {
        var httpClient = _httpClientFactory.CreateClient(AzureOpenAIConstants.HttpClientName);

        httpClient.BaseAddress = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", connection.GetApiKey());
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

        return httpClient;
    }

    private async Task<AzureCompletionResponse> GetResponseDataAsync(HttpClient httpClient, AzureCompletionRequest request, string deploymentName)
    {
        var payload = JsonContent.Create(request, options: _jsonSerializerOptions);

        var response = await httpClient.PostAsync($"openai/deployments/{deploymentName}/chat/completions?api-version=2024-05-01-preview", payload);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("Unable to create chat using Azure REST API. Content: {Content}", content);

            return null;
        }

        var data = await response.Content.ReadFromJsonAsync<AzureCompletionResponse>(_jsonSerializerOptions);

        if (data.Choices.Length > 0 && data.Choices[0].FinishReason == "function_call" && data.Choices[0].Message?.FunctionCall != null)
        {
            var message = data.Choices[0].Message;
            var function = _openAIFunctionService.FindByName(message.FunctionCall.Name);

            if (function is not null)
            {
                JsonObject arguments;

                try
                {
                    arguments = JsonSerializer.Deserialize<JsonObject>(message.FunctionCall.Arguments);
                }
                catch
                {
                    arguments = [];
                }

                var result = await function.InvokeAsync(arguments);

                string functionMessage;

                if (result is string str)
                {
                    functionMessage = str;
                }
                else
                {
                    functionMessage = JsonSerializer.Serialize(result, _jsonSerializerOptions);
                }

                request.Messages = request.Messages.Concat([OpenAIChatCompletionMessage.CreateFunctionMessage(functionMessage, message.FunctionCall.Name)]);

                data = await GetResponseDataAsync(httpClient, request, deploymentName);
            }
        }

        return data;
    }

    private async Task<(AzureCompletionRequest, bool)> BuildRequestAsync(OpenAIChatCompletionContext context, OpenAIChatProfileMetadata metadata, string[] functionNames, string systemMessage)
    {
        var request = new AzureCompletionRequest()
        {
            Temperature = metadata.Temperature ?? OpenAIConstants.DefaultTemperature,
            TopP = metadata.TopP ?? OpenAIConstants.DefaultTopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? OpenAIConstants.DefaultFrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? OpenAIConstants.DefaultPresencePenalty,
            MaxTokens = metadata.MaxTokens ?? OpenAIConstants.DefaultMaxTokens,
        };

        var includeContentItemCitations = false;

        if (functionNames != null && functionNames.Length > 0)
        {
            request.Functions = _openAIFunctionService.FindByNames(functionNames);
        }

        if (context.Profile.Source == AzureWithAzureAISearchProfileSource.Key &&
            context.Profile.TryGet<AzureAIChatProfileAISearchMetadata>(out var searchAIMetadata))
        {
            // Search against AISearch instance.
            var dataSource = new AzureCompletionDataSource()
            {
                Type = "azure_search",
                Parameters = [],
            };

            var azureAISearchIndexSettingsService = _serviceProvider.GetRequiredService<AzureAISearchIndexSettingsService>();

            var indexSettings = await azureAISearchIndexSettingsService.GetAsync(searchAIMetadata.IndexName);

            if (indexSettings == null || string.IsNullOrEmpty(indexSettings.IndexFullName) || !_azureAISearchDefaultOptions.ConfigurationExists())
            {
                return (request, includeContentItemCitations);
            }

            includeContentItemCitations = indexSettings.IndexedContentTypes is not null
                && indexSettings.IndexedContentTypes.Length > 0
                && searchAIMetadata.IncludeContentItemCitations;

            var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

            dataSource.Parameters["endpoint"] = _azureAISearchDefaultOptions.Endpoint;
            dataSource.Parameters["index_name"] = indexSettings.IndexFullName;
            dataSource.Parameters["semantic_configuration"] = "default";
            dataSource.Parameters["query_type"] = "simple";
            dataSource.Parameters["fields_mapping"] = GetFieldMapping(keyField);
            dataSource.Parameters["in_scope"] = true;
            dataSource.Parameters["role_information"] = systemMessage;
            dataSource.Parameters["filter"] = null;
            dataSource.Parameters["strictness"] = searchAIMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness;
            dataSource.Parameters["top_n_documents"] = searchAIMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;

            if (_azureAISearchDefaultOptions.Credential is not null)
            {
                dataSource.Parameters["authentication"] = new JsonObject()
                {
                    {"type","api_key"},
                    {"key", _azureAISearchDefaultOptions.Credential.Key},
                };
            }

            request.DataSources =
            [
                dataSource,
            ];
        }

        return (request, includeContentItemCitations);
    }

    private OpenAIChatCompletionResponse GetResponse(
        AzureCompletionResponse data,
        bool includeContentItemCitations,
        string userPrompt)
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

        var results = new List<OpenAIChatCompletionChoice>();

        foreach (var choice in data.Choices)
        {
            if (choice.Message?.Context?.Citations == null || choice.Message.Context.Citations.Length == 0 || !includeContentItemCitations)
            {
                results.Add(new OpenAIChatCompletionChoice()
                {
                    Content = Regex.Replace(choice.Message?.Content ?? string.Empty, @"\[doc\d+\]", string.Empty),
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
                            referenceBuilder.Append(CultureInfo.InvariantCulture, $" {referenceIndex}. <a href=\"{link}\" target=\"_blank\">{_htmlEncoder.Encode(referenceTitle)}</a> \r\n");
                        }
                    }
                }

                choiceMessage = choiceMessage.Replace(citationTemplate, needsReference ? $"<sup>{referenceIndex}</sup>" : string.Empty);
            }

            // During replacements, we could end up with multiple [--reference-separator--]
            // back to back. We can replace them with a single comma.
            responseChoice.Content = Regex.Replace(choiceMessage, @"(\[--reference-separator--\])+", "<sup>,</sup>")
                + "\r\n" + referenceBuilder.ToString();

            results.Add(responseChoice);
        }

        return new OpenAIChatCompletionResponse
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