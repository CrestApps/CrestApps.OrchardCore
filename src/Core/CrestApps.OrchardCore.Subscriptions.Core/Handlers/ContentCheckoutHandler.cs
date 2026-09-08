using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds a step for every content type a subscription plan asks the subscriber to fill in, and creates that
/// content when the checkout completes.
/// </summary>
/// <remarks>
/// The content is created in <see cref="CompletingAsync"/> rather than as the customer types, so a checkout
/// that is abandoned halfway leaves no half-finished published content behind.
/// </remarks>
public sealed class ContentCheckoutHandler : CheckoutHandlerBase
{
    /// <summary>
    /// The prefix that identifies a content-collection step, followed by the content type name.
    /// </summary>
    public const string ContentPrefix = "Content-";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentCheckoutHandler"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="documentJsonSerializerOptions">The document serializer options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentCheckoutHandler(
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<ContentCheckoutHandler> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!SubscriptionCheckout.IsSubscriptionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var contentItem = await _contentManager.GetAsync(session.ReferenceId);

        if (contentItem is null || !contentItem.TryGet<SubscriptionPart>(out _))
        {
            return;
        }

        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = typeDefinition?.Parts?.FirstOrDefault(part => part.Name == nameof(SubscriptionPart));

        if (partDefinition is null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<SubscriptionPartSettings>();

        if (settings.ContentTypes is null || settings.ContentTypes.Length == 0)
        {
            return;
        }

        for (var i = 0; i < settings.ContentTypes.Length; i++)
        {
            var contentType = settings.ContentTypes[i];

            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition is null)
            {
                continue;
            }

            var step = new CheckoutFlowStep
            {
                Key = $"{ContentPrefix}{contentType}",
                Title = definition.DisplayName,
                Description = S["Create a new {0}.", definition.DisplayName],
                CollectData = true,

                // Steps are numbered in tens so another handler can slot one in between without renumbering.
                Order = (i + 1) * 10,
            };

            step.Data.TryAdd("ContentType", contentType);

            session.Steps.Add(step);
        }
    }

    /// <inheritdoc/>
    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        foreach (var saved in context.Flow.Session.SavedSteps)
        {
            if (!saved.Key.StartsWith(ContentPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var step = saved.Value.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            if (step?.ContentItems is null)
            {
                continue;
            }

            foreach (var contentItem in step.ContentItems)
            {
                await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
                await _contentManager.PublishAsync(contentItem);
            }
        }
    }
}
