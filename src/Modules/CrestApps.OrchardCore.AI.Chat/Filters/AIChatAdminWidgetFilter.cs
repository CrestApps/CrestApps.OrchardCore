using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Settings;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Filters;

/// <summary>
/// Represents the AI chat admin widget filter.
/// </summary>
public sealed class AIChatAdminWidgetFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IResourceManager _resourceManager;
    private readonly IEnumerable<IAIChatPageContextProvider> _pageContextProviders;
    private readonly ILogger _logger;

    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatAdminWidgetFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="pageContextProviders">The providers of what the page is about.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="adminOptions">The admin options.</param>
    public AIChatAdminWidgetFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager,
        IAuthorizationService authorizationService,
        IResourceManager resourceManager,
        IEnumerable<IAIChatPageContextProvider> pageContextProviders,
        ILogger<AIChatAdminWidgetFilter> logger,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _profileManager = profileManager;
        _sessionManager = sessionManager;
        _authorizationService = authorizationService;
        _resourceManager = resourceManager;
        _pageContextProviders = pageContextProviders;
        _logger = logger;
        _adminOptions = adminOptions.Value;
    }

    /// <summary>
    /// Asynchronously performs the on result execution operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="next">The next.</param>
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

        if (settings is null || !settings.IsEnabled)
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

        var chatMode = ChatMode.TextInput;

        if (profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings))
        {
            chatMode = chatModeSettings.ChatMode;
        }

        var speechToTextEnabled = chatMode == ChatMode.AudioInput || chatMode == ChatMode.Conversation;

        _resourceManager.RegisterResource("stylesheet", "AIChatWidget").AtHead();
        _resourceManager.RegisterResource("stylesheet", "highlightjs").AtHead();
        _resourceManager.RegisterResource("stylesheet", "AIChatApp").AtHead();

        if (speechToTextEnabled)
        {
            _resourceManager.RegisterResource("stylesheet", "SpeechToText").AtHead();
        }

        _resourceManager.RegisterResource("script", "AIChatApp").AtFoot();
        _resourceManager.RegisterResource("script", "AIChatWidgetApp").AtFoot();

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
        shape.Properties["SendPageContext"] = settings.SendPageContext;
        shape.Properties["PageContext"] = settings.SendPageContext
            ? await GetPageContextAsync(context)
            : [];

        var layout = await _layoutAccessor.GetLayoutAsync();

        await layout.Zones["Footer"].AddAsync(shape, "999");

        await next();
    }

    private async Task<IReadOnlyList<CrestApps.Core.AI.Models.ChatClientContextEntry>> GetPageContextAsync(ResultExecutingContext context)
    {
        var pageContext = new AIChatPageContext(context.HttpContext, context.RouteData.Values);

        foreach (var provider in _pageContextProviders)
        {
            try
            {
                await provider.PopulateAsync(pageContext, context.HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // The page renders without that context rather than failing.
                _logger.LogWarning(ex, "The AI chat page context provider {Provider} failed.", provider.GetType().Name);
            }
        }

        return pageContext.Entries;
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
