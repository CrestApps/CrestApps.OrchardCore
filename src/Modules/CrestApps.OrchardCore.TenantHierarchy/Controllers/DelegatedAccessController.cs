using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// The parent endpoints of delegated access: open a child tenant, issue a one-time code, the embedded picker and
/// signing out everywhere. They answer only navigations, so a script cannot probe them.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Parent)]
public sealed class DelegatedAccessController : Controller
{
    private readonly ITenantHierarchyBroker _broker;
    private readonly DelegatedAccessIssuer _issuer;
    private readonly TenantSwitcherPreferenceStore _preferenceStore;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly IAuthorizationService _authorizationService;
    private readonly ShellSettings _shellSettings;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessController"/> class.
    /// </summary>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="issuer">The delegated access issuer.</param>
    /// <param name="preferenceStore">The tenant switcher preference store.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="adminOptions">The admin options.</param>
    public DelegatedAccessController(
        ITenantHierarchyBroker broker,
        DelegatedAccessIssuer issuer,
        TenantSwitcherPreferenceStore preferenceStore,
        HierarchyLabelsProvider labelsProvider,
        IAuthorizationService authorizationService,
        ShellSettings shellSettings,
        IOptions<AdminOptions> adminOptions)
    {
        _broker = broker;
        _issuer = issuer;
        _preferenceStore = preferenceStore;
        _labelsProvider = labelsProvider;
        _authorizationService = authorizationService;
        _shellSettings = shellSettings;
        _adminOptions = adminOptions.Value;
    }

    /// <summary>
    /// Starts entering a child tenant: checks the permission and the grant, then sends the browser to the child,
    /// which starts the sign-in.
    /// </summary>
    /// <param name="entryId">The registry entry of the child tenant.</param>
    /// <param name="returnUrl">A local URL of the child tenant to open after the sign-in.</param>
    [HttpGet]
    [Authorize]
    [Route(TenantHierarchyConstants.Routes.Open)]
    public async Task<IActionResult> Open(string entryId, string returnUrl = null)
    {
        if (!IsParent() || !FetchMetadataPolicy.IsNavigationOrUnknown(Request))
        {
            return NotFound();
        }

        SetNoStore();

        if (!await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.EnterChildTenants))
        {
            return Forbid();
        }

        var child = await _broker.GetChildAsync(entryId);

        if (child is null || !child.CanEnter || (await _issuer.GetGrantedRolesAsync(User, entryId)).Length == 0)
        {
            return ErrorView(EntryErrorKind.NoAccess);
        }

        if (_issuer.IsMfaRequiredAndMissing(User))
        {
            return ErrorView(EntryErrorKind.MfaRequired);
        }

        var target = $"{child.Address}/{TenantHierarchyConstants.Routes.Enter}";

        if (TenantHierarchyUrls.IsLocalUrl(returnUrl))
        {
            target += QueryString.Create("returnUrl", returnUrl);
        }

        return Redirect(target);
    }

    /// <summary>
    /// Issues a one-time code for the child tenant that started the sign-in and sends the browser back to the child's
    /// callback. The callback address comes from the child's shell settings, never from the request.
    /// </summary>
    /// <param name="client_id">The tenant identifier of the child tenant.</param>
    /// <param name="state">The state the child generated.</param>
    /// <param name="code_challenge">The PKCE code challenge.</param>
    /// <param name="code_challenge_method">The PKCE method. Only <c>S256</c> is supported.</param>
    [HttpGet]
    [Authorize]
    [Route(TenantHierarchyConstants.Routes.Authorize)]
#pragma warning disable IDE1006 // The parameter names are the OAuth 2.0 query parameter names.
    public async Task<IActionResult> Authorize(string client_id, string state, string code_challenge, string code_challenge_method)
#pragma warning restore IDE1006
    {
        if (!IsParent() || !FetchMetadataPolicy.IsNavigationOrUnknown(Request))
        {
            return NotFound();
        }

        SetNoStore();

        if (!string.Equals(code_challenge_method, "S256", StringComparison.Ordinal) ||
            !await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.EnterChildTenants))
        {
            return ErrorView(EntryErrorKind.NoAccess);
        }

        if (_issuer.IsMfaRequiredAndMissing(User))
        {
            return ErrorView(EntryErrorKind.MfaRequired);
        }

        var callback = await _issuer.IssueCodeAsync(User, client_id, code_challenge, state);

        return callback is null
            ? ErrorView(EntryErrorKind.NoAccess)
            : Redirect(callback);
    }

    /// <summary>
    /// Sends a parent user back to the child tenants admin, for the "Back to" link of the tenant switcher.
    /// </summary>
    [HttpGet]
    [Route("delegated-access/home")]
    public IActionResult Home()
    {
        if (!IsParent())
        {
            return NotFound();
        }

        return LocalRedirect($"~/{_adminOptions.AdminUrlPrefix}/children");
    }

    /// <summary>
    /// Sends a parent user to the hosted picker, for the "Switch" link of the tenant switcher.
    /// </summary>
    [HttpGet]
    [Route("delegated-access/switch")]
    public IActionResult Switch()
    {
        if (!IsParent())
        {
            return NotFound();
        }

        return LocalRedirect($"~/{_adminOptions.AdminUrlPrefix}/children/switch");
    }

    /// <summary>
    /// Renders the picker that a child tenant frames in its tenant switcher, when the parent policy uses the embedded
    /// mode. Only the requesting child may frame it, and only after its grant is checked; the list never enters the
    /// child's page, because the child cannot read a frame of another origin.
    /// </summary>
    /// <param name="child">The tenant identifier of the child tenant that frames the picker.</param>
    [HttpGet]
    [Authorize]
    [Route(TenantHierarchyConstants.Routes.EmbeddedPicker)]
    public async Task<IActionResult> Picker(string child)
    {
        if (!IsParent() || _shellSettings.GetParentPolicy().SwitcherMode != SwitcherMode.Embedded)
        {
            return NotFound();
        }

        SetNoStore();

        var enterable = await _issuer.GetEnterableChildrenAsync(User);
        var framing = enterable.FirstOrDefault(info => string.Equals(info.Entry.TenantId, child, StringComparison.Ordinal));

        if (framing is null || !await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.EnterChildTenants))
        {
            Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";

            return ErrorView(EntryErrorKind.NoAccess);
        }

        Response.Headers.ContentSecurityPolicy = $"frame-ancestors {framing.Address}";

        var model = await BuildSwitchModelAsync(enterable, embedded: true);
        model.CurrentEntryId = framing.Entry.EntryId;

        return View(model);
    }

    /// <summary>
    /// Marks a child tenant as a favorite of the current user, or removes the mark.
    /// </summary>
    /// <param name="entryId">The registry entry of the child tenant.</param>
    /// <param name="returnUrl">A local URL to return to.</param>
    [HttpPost]
    [Authorize]
    [Route("delegated-access/favorite/{entryId}")]
    public async Task<IActionResult> Favorite(string entryId, string returnUrl = null)
    {
        if (!IsParent())
        {
            return NotFound();
        }

        var enterable = await _issuer.GetEnterableChildrenAsync(User);

        if (!enterable.Any(info => info.Entry.EntryId == entryId))
        {
            return NotFound();
        }

        var isFavorite = await _preferenceStore.ToggleFavoriteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), entryId);

        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { isFavorite });
        }

        return TenantHierarchyUrls.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect($"~/{_adminOptions.AdminUrlPrefix}/children/switch");
    }

    /// <summary>
    /// Ends every delegated access session of the current user.
    /// </summary>
    /// <param name="returnUrl">A local URL to return to.</param>
    [HttpPost]
    [Authorize]
    [Route("delegated-access/sign-out-everywhere")]
    public async Task<IActionResult> SignOutEverywhere(string returnUrl = null)
    {
        if (!IsParent())
        {
            return NotFound();
        }

        var count = await _issuer.EndAllSessionsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        TempData["TenantHierarchy.SignedOutEverywhere"] = count;

        return TenantHierarchyUrls.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect($"~/{_adminOptions.AdminUrlPrefix}/children/switch");
    }

    internal async Task<TenantSwitchViewModel> BuildSwitchModelAsync(IReadOnlyList<ChildTenantInfo> enterable, bool embedded)
    {
        var preference = await _preferenceStore.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));

        return TenantSwitchViewModel.Create(enterable, preference, _labelsProvider.GetLabels(), embedded);
    }

    private ViewResult ErrorView(EntryErrorKind kind)
    {
        Response.StatusCode = kind == EntryErrorKind.MfaRequired
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status404NotFound;

        return View("EntryError", new EntryErrorViewModel
        {
            Kind = kind,
            Labels = _labelsProvider.GetLabels(),
            BackUrl = Url.Content($"~/{_adminOptions.AdminUrlPrefix}/children"),
        });
    }

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
    }

    private bool IsParent()
        => _shellSettings.IsParentTenant();
}
