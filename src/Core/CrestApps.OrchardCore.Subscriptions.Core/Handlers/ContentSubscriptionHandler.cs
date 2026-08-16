using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds subscription flow steps for content items that must be created after a subscription is purchased.
/// </summary>
public sealed class ContentSubscriptionHandler : SubscriptionHandlerBase
{
    /// <summary>
    /// The prefix used for flow step keys that collect content item data.
    /// </summary>
    public const string ContentPrefix = "Content-";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentSubscriptionHandler"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The manager used to read content type definitions.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used for persisted content step data.</param>
    /// <param name="contentManager">The content manager used to create and publish collected content items.</param>
    /// <param name="stringLocalizer">The localizer used for subscription flow step text.</param>
    public ContentSubscriptionHandler(
        IContentDefinitionManager contentDefinitionManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IContentManager contentManager,
        IStringLocalizer<ContentSubscriptionHandler> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Adds one data-collection step for each content type configured on the subscription part.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is being activated.</param>
    public override async Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out _))
        {
            return;
        }

        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.SubscriptionContentItem.ContentType);

        var partDefinition = typeDefinition?.Parts?.FirstOrDefault(x => x.Name == nameof(SubscriptionPart));

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

            var step = new SubscriptionFlowStep()
            {
                Title = definition.DisplayName,
                Description = S["Create a new {0}.", definition.DisplayName],
                Key = $"{ContentPrefix}{contentType}",
                CollectData = true,

                // Insert the steps using an increment of 10 for each step,
                // to allow other handler to inject steps in between if needed.
                Order = (i + 1) * 10,
            };

            step.Data.TryAdd("ContentType", contentType);

            context.Session.Steps.Add(step);
        }
    }

    /// <summary>
    /// Creates and publishes the content items collected by content subscription flow steps.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is completing.</param>
    public override async Task CompletingAsync(SubscriptionFlowCompletingContext context)
    {
        foreach (var item in context.Flow.Session.SavedSteps)
        {
            if (!item.Key.StartsWith(ContentPrefix))
            {
                continue;
            }

            var contentStep = item.Value.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            if (contentStep?.ContentItems == null)
            {
                continue;
            }

            foreach (var contentItem in contentStep.ContentItems)
            {
                await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
                await _contentManager.PublishAsync(contentItem);
            }
        }
    }
}
