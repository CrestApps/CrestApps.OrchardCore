using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Filters;

/// <summary>
/// Auto-injects the floating soft phone widget into the admin dashboard for users authorized to use the soft
/// phone, and registers the soft phone styles and scripts for those users on every page.
/// <para>
/// The front end no longer auto-injects the widget. An operator places the Soft Phone widget where they want
/// it through Design &gt; Widgets. This filter still registers the styles and scripts for authorized front-end
/// users, because the response head is written before a placed widget renders in the body, so the widget
/// shape itself cannot register a head stylesheet in time.
/// </para>
/// </summary>
public sealed class SoftPhoneWidgetFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISoftPhoneWidgetPresenter _presenter;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SoftPhoneWidget> _displayManager;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneWidgetFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="presenter">The soft phone widget presenter used to build the widget and register resources.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The soft phone widget display manager.</param>
    /// <param name="adminOptions">The admin options.</param>
    public SoftPhoneWidgetFilter(
        ILayoutAccessor layoutAccessor,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        ISoftPhoneWidgetPresenter presenter,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SoftPhoneWidget> displayManager,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _presenter = presenter;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
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

        // The admin dashboard auto-injects the phone when enabled; the front end never auto-injects it (the
        // operator places the Soft Phone widget), but its styles and scripts are still registered below.
        if (isAdmin && !settings.DisplayOnAdmin)
        {
            await next();

            return;
        }

        if (!await _authorizationService.AuthorizeAsync(context.HttpContext.User, TelephonyPermissions.UseSoftPhone))
        {
            await next();

            return;
        }

        var widget = await _presenter.CreateWidgetAsync();
        _presenter.RegisterResources(widget);

        // Signal that the soft phone is present on this request, so PhoneFieldDialerShapeTableProvider may add the
        // dialer "call" button when a phone field renders. The button is meaningless without the widget, so its
        // registration is gated on the same decision made here.
        context.HttpContext.Items[PhoneFieldDialerShapeTableProvider.SoftPhonePresentRequestKey] = true;

        if (isAdmin)
        {
            var shape = await _displayManager.BuildDisplayAsync(widget, _updateModelAccessor.ModelUpdater, "Detail");
            shape.Properties["AccentColor"] = widget.AccentColor;
            shape.Properties["Capabilities"] = (int)widget.Capabilities;
            shape.Properties["AudioCapabilities"] = (int)widget.AudioCapabilities;
            shape.Properties["AudioMode"] = (int)widget.AudioMode;
            shape.Properties["BrowserMediaAdapterName"] = widget.BrowserMediaAdapterName;
            shape.Properties["RecentCallsCount"] = widget.RecentCallsCount;
            shape.Properties["DefaultCountryCode"] = widget.DefaultCountryCode;

            var layout = await _layoutAccessor.GetLayoutAsync();

            await layout.Zones["Footer"].AddAsync(shape, "999");
        }

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        return context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
