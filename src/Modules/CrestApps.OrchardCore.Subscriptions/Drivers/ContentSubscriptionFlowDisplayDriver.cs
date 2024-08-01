using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public class ContentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    public ContentSubscriptionFlowDisplayDriver(
        IContentItemDisplayManager contentItemDisplayManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        IUpdateModelAccessor updateModelAccessor)
    {
        _contentItemDisplayManager = contentItemDisplayManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _updateModelAccessor = updateModelAccessor;
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

        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

        return Task.FromResult<IDisplayResult>(
            Factory("SubscriptionFlowContentItem", async (shapeBuildContext) =>
            {
                var contentItem = await contentManager.NewAsync(contentType.ToString());
                return await _contentItemDisplayManager.BuildEditorAsync(contentItem, context.Updater, true, string.Empty, flow.Session.CurrentStep);
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

        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.NewAsync(contentType.ToString());

        // If there is no logged in user, we'll fallback to the super user as an owner for this content.
        contentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? (await _siteService.GetSiteSettingsAsync()).SuperUser;

        await _contentItemDisplayManager.UpdateEditorAsync(contentItem, _updateModelAccessor.ModelUpdater, true, step.Key);

        flow.Session.SavedSteps[step.Key] = contentItem;

        return await EditAsync(flow, context);
    }
}
