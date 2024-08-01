using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class ContentSubscriptionHandler : SubscriptionHandlerBase
{
    public const string ContentPrefix = "Content-";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public ContentSubscriptionHandler(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IContentManager contentManager,
        IStringLocalizer<ContentSubscriptionHandler> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public override async Task InitializingAsync(SubscriptionFlowInitializationContext context)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.Flow.ContentItem.ContentType);

        var partDefinition = typeDefinition?.Parts?.FirstOrDefault(x => x.Name == nameof(SubscriptionsPart));

        if (partDefinition == null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<SubscriptionPartSettings>();

        if (settings.ContentTypes == null || settings.ContentTypes.Length == 0)
        {
            return;
        }

        for (var i = 0; i < settings.ContentTypes.Length; i++)
        {
            var contentType = settings.ContentTypes[i];

            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition == null)
            {
                continue;
            }

            // Insert the steps using an increment of 10 for each step,
            // to allow other handler to inject steps in between if needed.
            var order = (i + 1) * 10;

            var step = new SubscriptionFlowStep()
            {
                Title = definition.DisplayName,
                Description = S["Create a new {0}.", definition.DisplayName],
                Key = $"{ContentPrefix}{contentType}",
                Order = order,
            };

            step.Data.TryAdd("ContentType", contentType);

            context.Flow.Steps.Add(step);
        }
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        foreach (var item in context.Flow.Session.SavedSteps)
        {
            if (!item.Key.StartsWith(ContentPrefix))
            {
                continue;
            }

            var contentItem = item.Value as ContentItem;

            if (contentItem == null)
            {
                continue;
            }

            await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
            await _contentManager.PublishAsync(contentItem);
        }
    }
}
