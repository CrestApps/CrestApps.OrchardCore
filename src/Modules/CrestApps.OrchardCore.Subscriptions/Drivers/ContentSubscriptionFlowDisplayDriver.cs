using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public sealed class ContentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IServiceProvider _serviceProvider;

    public ContentSubscriptionFlowDisplayDriver(
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

    public override Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        var step = flow.GetCurrentStep();

        if (step == null ||
            flow.Session.CurrentStep == null ||
            !flow.Session.CurrentStep.StartsWith(ContentSubscriptionHandler.ContentPrefix) ||
            !step.Data.TryGetValue("ContentType", out var contentType))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return Task.FromResult<IDisplayResult>(
            Factory("SubscriptionFlowContentItem", async (shapeBuildContext) =>
            {
                var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

                return await _contentItemDisplayManager.BuildEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);
            }).Location("Content")
         );
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var step = flow.GetCurrentStep();

        if (step == null ||
            flow.Session.CurrentStep == null ||
            !flow.Session.CurrentStep.StartsWith(ContentSubscriptionHandler.ContentPrefix) ||
            !step.Data.TryGetValue("ContentType", out var contentType))
        {
            return null;
        }

        var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

        await _contentItemDisplayManager.UpdateEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);

        flow.Session.SavedSteps[step.Key] = JObject.FromObject(contentItem);

        return await EditAsync(flow, context);
    }

    private async Task<ContentItem> GetOrCreateContentItemAsync(SubscriptionFlow flow, SubscriptionFlowStep step, object contentType)
    {
        ContentItem contentItem = null;

        if (flow.Session.SavedSteps.TryGetPropertyValue(step.Key, out var node))
        {
            contentItem = node.Deserialize<ContentItem>(_documentJsonSerializerOptions.SerializerOptions);
        }

        if (contentItem == null)
        {
            // Lazily load content manager to avoid a possible circular references.
            var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

            contentItem = await contentManager.NewAsync(contentType.ToString());

            // If there is no logged in user, we'll fallback to the super user as an owner for this content.
            contentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? (await _siteService.GetSiteSettingsAsync()).SuperUser;
        }

        return contentItem;
    }
}
