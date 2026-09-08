using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Renders the editor for a content type a subscription plan collects during checkout.
/// </summary>
/// <remarks>
/// The content item is held on the session and only created when the checkout completes, so abandoning the
/// checkout leaves nothing published behind.
/// </remarks>
public sealed class ContentStepCheckoutFlowDisplayDriver : DisplayDriver<CheckoutFlow>
{
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentStepCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentItemDisplayManager">The content item display manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="siteService">The site service used to resolve a fallback owner.</param>
    /// <param name="documentJsonSerializerOptions">The document serializer options.</param>
    /// <param name="serviceProvider">The service provider used to resolve the content manager lazily.</param>
    public ContentStepCheckoutFlowDisplayDriver(
        IContentItemDisplayManager contentItemDisplayManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IServiceProvider serviceProvider)
    {
        _contentItemDisplayManager = contentItemDisplayManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!TryGetContentStep(flow, out var step, out var contentType))
        {
            return null;
        }

        return Factory("CheckoutFlowContentItem", async _ =>
        {
            var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

            return await _contentItemDisplayManager.BuildEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);
        }).Location("Content");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!TryGetContentStep(flow, out var step, out var contentType))
        {
            return null;
        }

        var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

        await _contentItemDisplayManager.UpdateEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);

        var stepInfo = new ContentStep
        {
            ContentItems = [contentItem],
        };

        flow.Session.SavedSteps[step.Key] = JObject.FromObject(stepInfo);

        return Edit(flow, context);
    }

    private static bool TryGetContentStep(CheckoutFlow flow, out CheckoutFlowStep step, out object contentType)
    {
        contentType = null;
        step = flow.GetCurrentStep();

        return step is not null &&
            flow.Session.CurrentStep is not null &&
            flow.Session.CurrentStep.StartsWith(ContentCheckoutHandler.ContentPrefix, StringComparison.Ordinal) &&
            step.Data.TryGetValue("ContentType", out contentType);
    }

    private async Task<ContentItem> GetOrCreateContentItemAsync(CheckoutFlow flow, CheckoutFlowStep step, object contentType)
    {
        ContentItem contentItem = null;

        if (flow.Session.SavedSteps.TryGetPropertyValue(step.Key, out var node))
        {
            var stepInfo = node.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            contentItem = stepInfo?.ContentItems?.FirstOrDefault();
        }

        if (contentItem is null)
        {
            // Resolved lazily to avoid a circular dependency between content display and this driver.
            var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

            contentItem = await contentManager.NewAsync(contentType.ToString());

            // A guest checkout has nobody to own the content, so it falls back to the site's super user
            // rather than being created ownerless.
            contentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? (await _siteService.GetSiteSettingsAsync()).SuperUser;
        }

        return contentItem;
    }
}
