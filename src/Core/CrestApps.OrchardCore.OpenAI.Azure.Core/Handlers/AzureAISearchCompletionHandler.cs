using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchCompletionHandler : IAICompletionHandler
{
    private readonly IAILinkGenerator _linkGenerator;

    public AzureAISearchCompletionHandler(IAILinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public Task ReceivedUpdateAsync(ReceivedUpdateContext context)
    {
        if (context.Update.AdditionalProperties is null ||
            !context.Update.AdditionalProperties.TryGetValue("ContentItemIds", out Dictionary<string, string> contentItemIds))
        {
            return Task.CompletedTask;
        }

        var routeValues = new Dictionary<string, object>()
        {
            { "prompt", context.Prompt },
        };

        var references = new Dictionary<string, string>();

        foreach (var (contentItemId, template) in contentItemIds)
        {
            references[template] = _linkGenerator.GetContentItemPath(contentItemId, routeValues);
        }

        context.Update.AdditionalProperties["References"] = references;

        return Task.CompletedTask;
    }
}
