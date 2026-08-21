using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

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
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhonePartDisplayDriver"/> class.
    /// </summary>
    /// <param name="presenter">The soft phone widget presenter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SoftPhonePartDisplayDriver(
        ISoftPhoneWidgetPresenter presenter,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _presenter = presenter;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(SoftPhonePart part, BuildPartDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.UseSoftPhone))
        {
            return null;
        }

        var widget = await _presenter.CreateWidgetAsync();

        return Initialize<SoftPhoneWidget>("SoftPhoneWidget", model =>
        {
            model.AccentColor = widget.AccentColor;
            model.Capabilities = widget.Capabilities;
            model.AudioCapabilities = widget.AudioCapabilities;
            model.AudioMode = widget.AudioMode;
            model.BrowserMediaAdapterName = widget.BrowserMediaAdapterName;
            model.RecentCallsCount = widget.RecentCallsCount;
            model.DefaultCountryCode = widget.DefaultCountryCode;
        }).Location("Detail", "Content:5");
    }
}
