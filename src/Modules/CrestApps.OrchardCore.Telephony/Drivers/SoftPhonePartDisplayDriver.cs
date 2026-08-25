using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Renders the floating soft phone for the Soft Phone widget content type. The widget is placed on the front
/// end through Design &gt; Widgets; this driver renders it only for authenticated users who are allowed to use
/// the soft phone. The styles and scripts are registered by <see cref="Filters.SoftPhoneWidgetFilter"/>, which
/// runs before the response head is written, so they are present regardless of where the widget is placed.
/// </summary>
public sealed class SoftPhonePartDisplayDriver : ContentPartDisplayDriver<SoftPhonePart>
{
    private readonly ISoftPhoneWidgetPresenter _presenter;
    private readonly IDisplayManager<SoftPhoneWidget> _softPhoneDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhonePartDisplayDriver"/> class.
    /// </summary>
    /// <param name="presenter">The soft phone widget presenter.</param>
    /// <param name="softPhoneDisplayManager">The soft phone widget display manager.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="siteService">The site service used to read the soft phone widget settings.</param>
    public SoftPhonePartDisplayDriver(
        ISoftPhoneWidgetPresenter presenter,
        IDisplayManager<SoftPhoneWidget> softPhoneDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService)
    {
        _presenter = presenter;
        _softPhoneDisplayManager = softPhoneDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> DisplayAsync(SoftPhonePart part, BuildPartDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.UseSoftPhone))
        {
            return null;
        }

        // Kill switch: a disabled soft phone does not render even where the widget is explicitly placed.
        var settings = await _siteService.GetSettingsAsync<SoftPhoneWidgetSettings>();

        if (settings is not null && !settings.Enabled)
        {
            return null;
        }

        // Build the soft phone shape exactly the way the admin filter does, through the SoftPhoneWidget display
        // manager, so it is a real dynamic shape whose HeaderActions/Views/Tabs extension zones are populated by
        // any contributing driver (and are simply null otherwise). Building a strongly-typed shape instead makes
        // those extension-zone reads throw, because the model does not declare them.
        return Factory("SoftPhoneWidget", async _ =>
        {
            var widget = await _presenter.CreateWidgetAsync();
            var shape = await _softPhoneDisplayManager.BuildDisplayAsync(widget, _updateModelAccessor.ModelUpdater, "Detail");

            shape.Properties["AccentColor"] = widget.AccentColor;
            shape.Properties["Capabilities"] = (int)widget.Capabilities;
            shape.Properties["AudioCapabilities"] = (int)widget.AudioCapabilities;
            shape.Properties["AudioMode"] = (int)widget.AudioMode;
            shape.Properties["BrowserMediaAdapterName"] = widget.BrowserMediaAdapterName;
            shape.Properties["RecentCallsCount"] = widget.RecentCallsCount;
            shape.Properties["DefaultCountryCode"] = widget.DefaultCountryCode;

            return shape;
        }).Location("Detail", "Content:5");
    }
}
