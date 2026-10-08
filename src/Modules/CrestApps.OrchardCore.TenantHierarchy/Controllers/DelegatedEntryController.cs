using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// The child endpoints of delegated access: start the sign-in, redeem the one-time code and sign out.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Child)]
public sealed class DelegatedEntryController : Controller
{
    private static readonly TimeSpan _stateLifetime = TimeSpan.FromMinutes(10);

    private readonly ITenantHierarchyBroker _broker;
    private readonly LinkedUserService _linkedUserService;
    private readonly SignInManager<IUser> _signInManager;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly ShellSettings _shellSettings;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedEntryController"/> class.
    /// </summary>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="linkedUserService">The linked user service.</param>
    /// <param name="signInManager">The sign-in manager.</param>
    /// <param name="dataProtectionProvider">The data protection provider of the child tenant.</param>
    /// <param name="shellSettings">The settings of the child tenant.</param>
    /// <param name="adminOptions">The admin options.</param>
    /// <param name="logger">The logger.</param>
    public DelegatedEntryController(
        ITenantHierarchyBroker broker,
        LinkedUserService linkedUserService,
        SignInManager<IUser> signInManager,
        IDataProtectionProvider dataProtectionProvider,
        ShellSettings shellSettings,
        IOptions<AdminOptions> adminOptions,
        ILogger<DelegatedEntryController> logger)
    {
        _broker = broker;
        _linkedUserService = linkedUserService;
        _signInManager = signInManager;
        _protector = dataProtectionProvider.CreateProtector("TenantHierarchy.DelegatedAccess.State").ToTimeLimitedDataProtector();
        _shellSettings = shellSettings;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Starts the sign-in: remembers a state and a PKCE verifier in an encrypted cookie bound to this browser, then
    /// sends the browser to the parent, which issues a one-time code.
    /// </summary>
    /// <param name="returnUrl">A local URL to open after the sign-in.</param>
    [HttpGet]
    [AllowAnonymous]
    [Route(TenantHierarchyConstants.Routes.Enter)]
    public async Task<IActionResult> Enter(string returnUrl = null)
    {
        if (!_shellSettings.IsChildTenant() || !FetchMetadataPolicy.IsNavigationOrUnknown(Request))
        {
            return NotFound();
        }

        SetNoStore();

        var parentAddress = await _broker.GetParentAddressAsync();

        if (string.IsNullOrEmpty(parentAddress))
        {
            return ErrorView(EntryErrorKind.NoAccess, null);
        }

        var state = new DelegatedAccessState
        {
            State = DelegatedAccessTokens.CreateToken(),
            CodeVerifier = DelegatedAccessTokens.CreateToken(),
            ReturnUrl = TenantHierarchyUrls.IsLocalUrl(returnUrl) ? returnUrl : null,
        };

        Response.Cookies.Append(
            GetStateCookieName(),
            _protector.Protect(JsonSerializer.Serialize(state), _stateLifetime),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = _stateLifetime,
            });

        var query = QueryString.Create(new Dictionary<string, string>
        {
            ["client_id"] = _shellSettings.TenantId,
            ["state"] = state.State,
            ["code_challenge"] = DelegatedAccessTokens.CreateCodeChallenge(state.CodeVerifier),
            ["code_challenge_method"] = "S256",
        });

        return Redirect($"{parentAddress}/{TenantHierarchyConstants.Routes.Authorize}{query}");
    }

    /// <summary>
    /// Redeems the one-time code: the state must match the cookie of this browser, the code is redeemed through the
    /// broker with the PKCE verifier, then the linked user is signed in.
    /// </summary>
    /// <param name="code">The one-time code.</param>
    /// <param name="state">The state returned by the parent.</param>
    [HttpGet]
    [AllowAnonymous]
    [Route(TenantHierarchyConstants.Routes.Callback)]
    public async Task<IActionResult> Callback(string code, string state)
    {
        if (!_shellSettings.IsChildTenant() || !FetchMetadataPolicy.IsNavigationOrUnknown(Request))
        {
            return NotFound();
        }

        SetNoStore();

        var remembered = ReadState();
        Response.Cookies.Delete(GetStateCookieName(), new CookieOptions
        {
            Path = "/",
            Secure = Request.IsHttps,
        });

        if (remembered is null || string.IsNullOrEmpty(state) || !FixedTimeEquals(remembered.State, state))
        {
            return ErrorView(EntryErrorKind.SignInExpired, null);
        }

        var redemption = await _broker.RedeemCodeAsync(code, remembered.CodeVerifier);

        if (redemption is null)
        {
            return ErrorView(EntryErrorKind.NoAccess, null);
        }

        var user = await _linkedUserService.ProvisionAsync(redemption);

        if (User.Identity?.IsAuthenticated == true)
        {
            await _signInManager.SignOutAsync();
        }

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, DelegatedAccessClaims.Create(redemption));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Parent user '{ParentUserId}' entered child tenant '{ChildTenant}' as linked user '{UserId}'.",
                redemption.ParentUserId,
                _shellSettings.Name,
                user.UserId);
        }

        return LocalRedirect(remembered.ReturnUrl ?? $"~/{_adminOptions.AdminUrlPrefix}");
    }

    /// <summary>
    /// Ends the delegated access session and sends the user back to the parent.
    /// </summary>
    [HttpPost]
    [Route(TenantHierarchyConstants.Routes.SignOut)]
    public async Task<IActionResult> EndSession()
    {
        if (!_shellSettings.IsChildTenant())
        {
            return NotFound();
        }

        var parentAddress = User.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentAddress)?.Value;

        await _signInManager.SignOutAsync();

        return string.IsNullOrEmpty(parentAddress) || !Uri.TryCreate(parentAddress, UriKind.Absolute, out _)
            ? LocalRedirect("~/")
            : Redirect($"{parentAddress}/delegated-access/home");
    }

    private DelegatedAccessState ReadState()
    {
        if (!Request.Cookies.TryGetValue(GetStateCookieName(), out var protectedState) || string.IsNullOrEmpty(protectedState))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DelegatedAccessState>(_protector.Unprotect(protectedState));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string GetStateCookieName()
    {
        // A browser accepts a __Host- cookie only over HTTPS.
        return Request.IsHttps
            ? TenantHierarchyConstants.Cookies.StateCookieName
            : TenantHierarchyConstants.Cookies.InsecureStateCookieName;
    }

    private ViewResult ErrorView(EntryErrorKind kind, string backUrl)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;

        return View("EntryError", new EntryErrorViewModel
        {
            Kind = kind,
            BackUrl = backUrl,
            RetryUrl = kind == EntryErrorKind.SignInExpired ? Url.Content($"~/{TenantHierarchyConstants.Routes.Enter}") : null,
        });
    }

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected ?? string.Empty), Encoding.UTF8.GetBytes(actual ?? string.Empty));
    }
}
