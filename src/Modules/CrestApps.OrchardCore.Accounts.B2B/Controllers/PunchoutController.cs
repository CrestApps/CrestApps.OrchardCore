using CrestApps.Core.Support;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement;
using OrchardCore.Users;

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
    private readonly ICartService _cartService;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILogger _logger;

    public PunchoutController(
        IB2BAccountStore accounts,
        PunchoutService punchout,
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        ICartService cartService,
        IShapeFactory shapeFactory,
        ILogger<PunchoutController> logger)
    {
        _accounts = accounts;
        _punchout = punchout;
        _userManager = userManager;
        _signInManager = signInManager;
        _cartService = cartService;
        _shapeFactory = shapeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts a punchout. The procurement system posts (or links with) the account's key as <c>PASSWORD</c> or
    /// <c>key</c>, and <c>HOOK_URL</c>, where the cart goes back to; <c>~TARGET</c> names the frame it opens in.
    /// </summary>
    [HttpGet("punchout/oci/{accountId}")]
    [HttpPost("punchout/oci/{accountId}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Start(string accountId)
    {
        var form = Request.HasFormContentType ? await Request.ReadFormAsync(HttpContext.RequestAborted) : null;
        string Value(string name) => form?[name].FirstOrDefault() ?? Request.Query[name].FirstOrDefault();

        var key = Value("PASSWORD") ?? Value("key");
        var hookUrl = PunchoutService.ParseHookUrl(Value("HOOK_URL"));

        if (await _accounts.FindByIdAsync(accountId, HttpContext.RequestAborted) is not { IsEnabled: true } account ||
            !PunchoutService.IsKey(account, key) || hookUrl is null ||
            account.FindBuyer(account.Punchout.UserId) is null ||
            await _userManager.FindByIdAsync(account.Punchout.UserId) is not { } user ||
            await _userManager.IsInRoleAsync(user, "Administrator"))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("A punchout for account '{AccountId}' was refused: unknown account, wrong key, a missing HOOK_URL or no punchout buyer.", accountId?.SanitizeLogValue());
            }

            return Unauthorized();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        _punchout.Start(account.AccountId, hookUrl, Value("~TARGET"));

        return Redirect(Url.Content("~/"));
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
