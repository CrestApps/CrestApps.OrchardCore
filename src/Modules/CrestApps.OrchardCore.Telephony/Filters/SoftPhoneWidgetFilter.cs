using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Filters;

/// <summary>
/// Injects the floating soft phone widget into the admin dashboard and/or the front end based on the
/// soft phone widget settings, for users authorized to use the soft phone.
/// </summary>
public sealed class SoftPhoneWidgetFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly IResourceManager _resourceManager;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneWidgetFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="adminOptions">The admin options.</param>
    public SoftPhoneWidgetFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        ITelephonyProviderResolver providerResolver,
        IResourceManager resourceManager,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _providerResolver = providerResolver;
        _resourceManager = resourceManager;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not (ViewResult or PageResult) || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();

            return;
        }

        var settings = await _siteService.GetSettingsAsync<SoftPhoneWidgetSettings>();

        if (settings is null)
        {
            await next();

            return;
        }

        var isAdmin = IsAdminPage(context);
        var isEnabled = isAdmin ? settings.DisplayOnAdmin : settings.DisplayOnFrontend;

        if (!isEnabled)
        {
            await next();

            return;
        }

        if (!await _authorizationService.AuthorizeAsync(context.HttpContext.User, TelephonyPermissions.UseSoftPhone))
        {
            await next();

            return;
        }

        var provider = await _providerResolver.GetAsync();
        var capabilities = provider is not null ? (int)provider.Capabilities : 0;

        _resourceManager.RegisterResource("stylesheet", "telephony-soft-phone").AtHead();
        _resourceManager.RegisterResource("script", "telephony-soft-phone").AtFoot();
        _resourceManager.RegisterResource("script", "telephony-phone-field").AtFoot();

        var shape = await _shapeFactory.CreateAsync("SoftPhoneWidget");

        shape.Properties["AccentColor"] = string.IsNullOrWhiteSpace(settings.AccentColor)
            ? SoftPhoneWidgetSettings.DefaultAccentColor
            : settings.AccentColor;
        shape.Properties["Capabilities"] = capabilities;

        var layout = await _layoutAccessor.GetLayoutAsync();

        await layout.Zones["Footer"].AddAsync(shape, "999");

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        return context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
