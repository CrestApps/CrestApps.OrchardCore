using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class ContentSubscriptionHandler : SubscriptionHandlerBase
{
    public const string ContentPrefix = "Content-";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public ContentSubscriptionHandler(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IContentManager contentManager,
        IStringLocalizer<ContentSubscriptionHandler> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public override async Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        if (!context.SubscriptionContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
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
                Plan = new SubscriptionPlan()
                {
                    Description = context.SubscriptionContentItem.DisplayText,
                    Id = context.Session.ContentItemVersionId,
                    InitialAmount = subscriptionPart.InitialAmount,
                    BillingAmount = subscriptionPart.BillingAmount,
                    SubscriptionDayDelay = subscriptionPart.SubscriptionDayDelay,
                    BillingDuration = subscriptionPart.BillingDuration,
                    DurationType = subscriptionPart.DurationType,
                    BillingCycleLimit = subscriptionPart.BillingCycleLimit,
                },

                // Insert the steps using an increment of 10 for each step,
                // to allow other handler to inject steps in between if needed.
                Order = (i + 1) * 10,
            };

            step.Data.TryAdd("ContentType", contentType);

            context.Session.Steps.Add(step);
        }
    }

    public override async Task CompletingAsync(SubscriptionFlowCompletedContext context)
    {
        foreach (var item in context.Flow.Session.SavedSteps)
        {
            if (!item.Key.StartsWith(ContentPrefix))
            {
                continue;
            }

            var contentStep = item.Value.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            if (contentStep == null)
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
