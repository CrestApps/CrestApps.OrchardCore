using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIDataSourceCompletionClient : OpenAICompletionClient
{
    private readonly IAILinkGenerator _aiLinkGenerator;

    public AzureOpenAIDataSourceCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions,
        INamedCatalog<AIDeployment> deploymentStore,
        IAILinkGenerator aILinkGenerator,
        IEnumerable<IOpenAIChatOptionsConfiguration> openAIChatOptionsConfigurations
        ) : base(
            AzureOpenAIConstants.AzureOpenAIOwnData,
            aIClientFactory,
            loggerFactory,
            distributedCache,
            providerOptions.Value,
            handlers,
            defaultOptions.Value,
            deploymentStore,
            openAIChatOptionsConfigurations)
    {
        _aiLinkGenerator = aILinkGenerator;
    }

    protected override string ProviderName
        => AzureOpenAIConstants.ProviderName;

    protected override void ProcessChatResponseUpdate(ChatResponseUpdate update, IEnumerable<Microsoft.Extensions.AI.ChatMessage> prompts)
    {
        if (update.RawRepresentation is not StreamingChatCompletionUpdate openAIUpdate)
        {
            return;
        }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var updateContext = openAIUpdate.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        if (updateContext?.Citations is not null && updateContext.Citations.Count > 0)
        {
            HashSet<string> contentItemIds;
            Dictionary<string, AICompletionReference> references;
            GetReferences(prompts, linkContext: null, updateContext, out contentItemIds, out references);

            update.AdditionalProperties ??= [];
            update.AdditionalProperties["ContentItemIds"] = contentItemIds;
            update.AdditionalProperties["References"] = references;
        }
    }

    protected override void ProcessChatResponse(ChatResponse response, IEnumerable<Microsoft.Extensions.AI.ChatMessage> prompts)
    {
        if (response.RawRepresentation is not ChatCompletion chatCompletion)
        {
            return;
        }

        Dictionary<string, object> linkContext = null;

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var updateContext = chatCompletion.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        if (updateContext?.Citations is not null && updateContext.Citations.Count > 0)
        {
            HashSet<string> contentItemIds;
            Dictionary<string, AICompletionReference> references;
            GetReferences(prompts, linkContext, updateContext, out contentItemIds, out references);

            response.AdditionalProperties ??= [];
            response.AdditionalProperties["ContentItemIds"] = contentItemIds;
            response.AdditionalProperties["References"] = references;
        }
    }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    private void GetReferences(IEnumerable<Microsoft.Extensions.AI.ChatMessage> prompts, Dictionary<string, object> linkContext, ChatMessageContext updateContext, out HashSet<string> contentItemIds, out Dictionary<string, AICompletionReference> references)
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        linkContext ??= new Dictionary<string, object>
        {
            { "prompt", prompts.LastOrDefault(x => x.Role == ChatRole.User)?.Text },
        };

        contentItemIds = [];
        references = [];
        foreach (var citation in updateContext.Citations)
        {
            if (string.IsNullOrEmpty(citation.FilePath))
            {
                continue;
            }

            contentItemIds.Add(citation.FilePath);
            var templateIndex = references.Count + 1;

            var template = $"[doc{templateIndex}]";

            references[template] = new AICompletionReference
            {
                Text = string.IsNullOrEmpty(citation.Title) ? template : citation.Title,
                Index = templateIndex,
                Link = _aiLinkGenerator.GetContentItemPath(citation.FilePath, linkContext),
                Title = citation.Title,
            };
        }
    }
}
