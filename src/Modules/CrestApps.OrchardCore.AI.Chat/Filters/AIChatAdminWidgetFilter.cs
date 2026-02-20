using CrestApps.OrchardCore.AI.Chat.Settings;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Filters;

public sealed class AIChatAdminWidgetFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly AdminOptions _adminOptions;

    public AIChatAdminWidgetFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _adminOptions = adminOptions.Value;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!IsAdminPage(context))
        {
            await next();
            return;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var settings = await _siteService.GetSettingsAsync<AIChatAdminWidgetSettings>();

        if (string.IsNullOrEmpty(settings?.ProfileId))
        {
            await next();
            return;
        }

        var profile = await _profileManager.FindByIdAsync(settings.ProfileId);
        if (profile == null)
        {
            await next();
            return;
        }

        if (!await _authorizationService.AuthorizeAsync(context.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            await next();
            return;
        }

        var sessionResult = await _sessionManager.PageAsync(
            page: 1,
            pageSize: settings.MaxSessions,
            new AIChatSessionQueryContext
            {
                ProfileId = settings.ProfileId,
                Sorted = true,
            });

        var shape = await _shapeFactory.CreateAsync("AIChatAdminWidget");
        shape.Properties["Profile"] = profile;
        shape.Properties["Sessions"] = sessionResult?.Sessions ?? [];
        shape.Properties["MaxSessions"] = settings.MaxSessions;
        shape.Properties["PrimaryColor"] = string.IsNullOrWhiteSpace(settings.PrimaryColor)
            ? AIChatAdminWidgetSettings.DefaultPrimaryColor
            : settings.PrimaryColor;

        var layout = await _layoutAccessor.GetLayoutAsync();
        await layout.Zones["Footer"].AddAsync(shape, "999");

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        if (context.Result is not (ViewResult or PageResult))
        {
            return false;
        }

        return context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
