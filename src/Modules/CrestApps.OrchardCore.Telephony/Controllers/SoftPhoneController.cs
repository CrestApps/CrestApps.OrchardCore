using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Controllers;

/// <summary>
/// Serves the standalone <c>/softphone</c> page hosted by the CrestApps Soft Phone browser extension. The
/// page renders the same soft phone the widget uses, chromeless and full-window, so a WebRTC call survives
/// the agent navigating other sites. Unauthenticated requests are redirected to the login and returned here.
/// </summary>
[Authorize]
[Feature(TelephonyConstants.Feature.SoftPhoneExtension)]
public sealed class SoftPhoneController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISoftPhoneWidgetPresenter _presenter;
    private readonly IDisplayManager<SoftPhoneWidget> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="presenter">The soft phone widget presenter, shared with the widget feature.</param>
    /// <param name="displayManager">The soft phone widget display manager.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    public SoftPhoneController(
        IAuthorizationService authorizationService,
        ISoftPhoneWidgetPresenter presenter,
        IDisplayManager<SoftPhoneWidget> displayManager,
        IUpdateModelAccessor updateModelAccessor)
    {
        _authorizationService = authorizationService;
        _presenter = presenter;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
    }

    /// <summary>
    /// Renders the standalone soft phone page.
    /// </summary>
    /// <param name="answerCallId">The optional pending-offer call id to auto-answer on load.</param>
    /// <returns>The chromeless full-window soft phone view.</returns>
    // The view sets Layout = null so the page renders chromeless (no theme layout), filling the extension
    // window; it emits its own <head>/<body> and renders the registered soft phone resources itself.
    [Route("softphone", Name = "TelephonySoftPhonePage")]
    public async Task<IActionResult> Index(string answerCallId = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonyPermissions.UseSoftPhone))
        {
            return Forbid();
        }

        var widget = await _presenter.CreateWidgetAsync();
        _presenter.RegisterResources(widget);

        var shape = await _displayManager.BuildDisplayAsync(widget, _updateModelAccessor.ModelUpdater, "Detail");
        shape.Properties["AccentColor"] = widget.AccentColor;
        shape.Properties["Capabilities"] = (int)widget.Capabilities;
        shape.Properties["AudioCapabilities"] = (int)widget.AudioCapabilities;
        shape.Properties["AudioMode"] = (int)widget.AudioMode;
        shape.Properties["BrowserMediaAdapterName"] = widget.BrowserMediaAdapterName;
        shape.Properties["RecentCallsCount"] = widget.RecentCallsCount;
        shape.Properties["DefaultCountryCode"] = widget.DefaultCountryCode;
        shape.Properties["EnableDiagnostics"] = widget.EnableDiagnostics;

        // Tell the shape template to render the phone expanded and full-window instead of the floating panel.
        shape.Properties["Embedded"] = true;

        return View(new SoftPhoneStandaloneViewModel
        {
            Shape = shape,
            AnswerCallId = answerCallId,
            Embedded = true,
        });
    }
}
