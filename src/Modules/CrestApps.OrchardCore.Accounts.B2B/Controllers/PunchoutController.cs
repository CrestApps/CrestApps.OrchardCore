using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Accounts.B2B.Controllers;

/// <summary>
/// OCI punchout: an account's procurement system opens the store at <c>punchout/oci/{accountId}</c>, and the buyer sends
/// their cart back to it from <c>punchout/oci/transfer</c>.
/// </summary>
public sealed class PunchoutController : Controller
{
    private readonly IB2BAccountStore _accounts;
    private readonly PunchoutService _punchout;
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICartService _cartService;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILogger _logger;

    public PunchoutController(
        IB2BAccountStore accounts,
        PunchoutService punchout,
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        IAuthorizationService authorizationService,
        ICartService cartService,
        IShapeFactory shapeFactory,
        ILogger<PunchoutController> logger)
    {
        _accounts = accounts;
        _punchout = punchout;
        _userManager = userManager;
        _signInManager = signInManager;
        _authorizationService = authorizationService;
        _cartService = cartService;
        _shapeFactory = shapeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts a punchout. The procurement system posts the account's key as <c>PASSWORD</c> or <c>key</c>, and
    /// <c>HOOK_URL</c>, where the cart goes back to; <c>~TARGET</c> names the frame it opens in. A link (a GET with the
    /// key in the address) is accepted only from an account that allows OCI link mode.
    /// </summary>
    [HttpGet("punchout/oci/{accountId}")]
    [HttpPost("punchout/oci/{accountId}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Start(string accountId)
    {
        var isPost = HttpMethods.IsPost(Request.Method);
        var form = isPost && Request.HasFormContentType ? await Request.ReadFormAsync(HttpContext.RequestAborted) : null;
        string Value(string name) => isPost ? form?[name].FirstOrDefault() : Request.Query[name].FirstOrDefault();

        var account = await _accounts.FindByIdAsync(accountId, HttpContext.RequestAborted);

        // A key in the address lands in access logs and referrers, and a link lets any page sign its visitor in as the
        // punchout buyer; only an account that needs OCI link mode turns it on.
        if (!isPost && account?.Punchout?.AllowLinkSignIn != true)
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        var key = Value("PASSWORD") ?? Value("key");
        var hookUrl = PunchoutService.ParseHookUrl(Value("HOOK_URL"));

        if (account is not { IsEnabled: true } ||
            !PunchoutService.IsKey(account, key) || hookUrl is null ||
            account.FindBuyer(account.Punchout.UserId) is null ||
            await _userManager.FindByIdAsync(account.Punchout.UserId) is not { } user ||
            !await MaySignInAsync(user))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                // The account's id stays out of the log, like every other record identifier.
                _logger.LogWarning("A punchout was refused: unknown account, wrong key, a missing HOOK_URL or no punchout buyer.");
            }

            return Unauthorized();
        }

        // The session says it is a punchout, so it can be recognized and limited.
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim(PunchoutService.ClaimType, account.AccountId)]);
        _punchout.Start(account.AccountId, hookUrl, Value("~TARGET"));

        return Redirect(Url.Content("~/"));
    }

    // The key signs in only a buyer who could sign in themselves (enabled, not locked out, confirmed as the site
    // requires) and who can't reach the admin: a punchout session is for shopping, never for running the store.
    private async Task<bool> MaySignInAsync(IUser user)
    {
        if (user is User { IsEnabled: false } ||
            await _userManager.IsLockedOutAsync(user) ||
            !await _signInManager.CanSignInAsync(user))
        {
            return false;
        }

        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        return !await _authorizationService.AuthorizeAsync(principal, AdminPermissions.AccessAdminPanel);
    }

    /// <summary>
    /// Sends the cart back to the procurement system: a form of OCI fields posted to its HOOK_URL.
    /// </summary>
    [HttpGet("punchout/oci/transfer", Name = "PunchoutTransfer")]
    public async Task<IActionResult> Transfer()
    {
        if (_punchout.GetCurrent() is not { } session || User.Identity?.IsAuthenticated != true)
        {
            return NotFound();
        }

        var cart = await _cartService.GetCurrentAsync(HttpContext.RequestAborted);
        var pricing = await _cartService.PriceAsync(cart, HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<PunchoutTransferViewModel>("PunchoutTransfer", model =>
        {
            model.HookUrl = session.HookUrl;
            model.Target = session.Target;
            model.Fields = PunchoutService.ToOciFields(pricing);
            model.ItemCount = pricing.ItemCount;
        }));
    }
}
