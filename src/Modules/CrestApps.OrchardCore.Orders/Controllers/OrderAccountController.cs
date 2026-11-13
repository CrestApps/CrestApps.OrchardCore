using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.Users;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Makes an account for a guest from their order's confirmation. The account takes the order's email and gets no
/// password: a link sent to the address lets its owner choose one, and only then does the order join the account.
/// Earlier guest orders with the email join when they sign in.
/// </summary>
public sealed class OrderAccountController : Controller
{
    /// <summary>
    /// The user property that names the order an account was made from, until its link is followed.
    /// </summary>
    public const string PendingOrderPropertyName = "OrderAccountPendingOrderId";

    private const string ActivateRouteName = "OrderAccountActivate";

    private readonly IOrderStore _orderStore;
    private readonly OrderAccountClaims _claims;
    private readonly UserManager<IUser> _userManager;
    private readonly IAttemptLimiter _limiter;
    private readonly INotifier _notifier;
    private readonly ICommerceEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public OrderAccountController(
        IOrderStore orderStore,
        OrderAccountClaims claims,
        UserManager<IUser> userManager,
        IAttemptLimiter limiter,
        INotifier notifier,
        ICommerceEmailSender emailSender,
        IClock clock,
        IShapeFactory shapeFactory,
        ILogger<OrderAccountController> logger,
        IStringLocalizer<OrderAccountController> stringLocalizer,
        IHtmlLocalizer<OrderAccountController> htmlLocalizer)
    {
        _orderStore = orderStore;
        _claims = claims;
        _userManager = userManager;
        _limiter = limiter;
        _notifier = notifier;
        _emailSender = emailSender;
        _clock = clock;
        _shapeFactory = shapeFactory;
        _logger = logger;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [HttpPost("orders/create-account", Name = "OrderCreateAccount")]
    [RateLimitGroup(OrderConstants.FindRateLimitGroup)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateAccountViewModel model, string returnUrl)
    {
        var back = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
        var orderId = _claims.Read(model.Token);
        var order = string.IsNullOrEmpty(orderId) ? null : await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted);

        if (order is null || order.UserId is not null || string.IsNullOrWhiteSpace(order.CustomerEmail) || User.Identity?.IsAuthenticated == true)
        {
            await _notifier.ErrorAsync(H["This order can no longer be saved to a new account. Sign in, or create an account with the same email, to see your orders."]);

            return LocalRedirect(back);
        }

        if (!(await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderCreateAccount, ClientNetworkKey.NetworkOnly(HttpContext))).Allowed)
        {
            await _notifier.ErrorAsync(H["Too many attempts. Wait a few minutes, then try again."]);

            return LocalRedirect(back);
        }

        // Without email the account could never be confirmed, and the buyer would be told a link was sent.
        if (!_emailSender.CanSend)
        {
            await _notifier.ErrorAsync(H["An account can't be made from this page right now. Register with the same email, and your orders appear in your account."]);

            return LocalRedirect(back);
        }

        var email = order.CustomerEmail.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            await _notifier.InformationAsync(H["There's already an account with {0}. Sign in, and your orders with this email appear in your account.", email]);

            return LocalRedirect(back);
        }

        var registration = HttpContext.RequestServices.GetService<IOptions<RegistrationOptions>>()?.Value;

        // The address comes from the order, which anyone could have typed (SEC-C17h). So the account has no password until
        // the link sent to the address is followed, and the password is chosen there: whoever typed someone else's
        // address can't sign in to the account, and the owner who follows the link owns the only password it ever had.
        // The order joins the account then too, not now.
        var user = new User
        {
            UserName = await NewUserNameAsync(email),
            Email = email,
            EmailConfirmed = false,
            IsEnabled = registration?.UsersAreModerated != true,
        };

        user.Properties[PendingOrderPropertyName] = order.ItemId;

        var result = await _userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["Your account wasn't created: {0}", string.Join(" ", result.Errors.Select(error => error.Description))]);

            return LocalRedirect(back);
        }

        if (!await SendActivationAsync(user))
        {
            await _notifier.ErrorAsync(H["Your account was created, but the email to finish it couldn't be sent. Use \"Forgot password\" with {0} to finish it.", email]);

            return LocalRedirect(back);
        }

        await _notifier.SuccessAsync(H["We sent a link to {0}. Open it to choose your password and finish your account.", email]);

        return LocalRedirect(back);
    }

    /// <summary>
    /// The page the emailed link opens: the owner of the address chooses the account's password.
    /// </summary>
    [HttpGet("orders/account/activate", Name = ActivateRouteName)]
    public async Task<IActionResult> Activate(string userId, string code)
    {
        if (await FindPendingAsync(userId) is null || string.IsNullOrEmpty(code))
        {
            await _notifier.ErrorAsync(H["This link no longer works. Sign in, or use \"Forgot password\"."]);

            return LocalRedirect(Url.Content("~/"));
        }

        return await ActivatePageAsync(new OrderAccountActivateViewModel { UserId = userId, Code = code });
    }

    [HttpPost("orders/account/activate")]
    [RateLimitGroup(OrderConstants.FindRateLimitGroup)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(OrderAccountActivateViewModel model)
    {
        if (!(await _limiter.AcquireAsync(OrderConstants.AttemptPolicies.OrderCreateAccount, ClientNetworkKey.NetworkOnly(HttpContext))).Allowed)
        {
            ModelState.AddModelError(string.Empty, S["Too many attempts. Wait a few minutes, then try again."]);

            return await ActivatePageAsync(model);
        }

        if (await FindPendingAsync(model?.UserId) is not { } user || string.IsNullOrEmpty(model.Code))
        {
            await _notifier.ErrorAsync(H["This link no longer works. Sign in, or use \"Forgot password\"."]);

            return LocalRedirect(Url.Content("~/"));
        }

        if (string.IsNullOrEmpty(model.Password) || !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), S["The two passwords don't match."]);

            return await ActivatePageAsync(model);
        }

        // Checked before the link is used, so a refused password can be chosen again with the same link.
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, user, model.Password);

            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(nameof(model.Password), error.Description);
            }
        }

        if (!ModelState.IsValid)
        {
            return await ActivatePageAsync(model);
        }

        if (!(await _userManager.ConfirmEmailAsync(user, model.Code)).Succeeded)
        {
            await _notifier.ErrorAsync(H["This link no longer works. Sign in, or use \"Forgot password\"."]);

            return LocalRedirect(Url.Content("~/"));
        }

        var added = await _userManager.AddPasswordAsync(user, model.Password);

        if (!added.Succeeded)
        {
            foreach (var error in added.Errors)
            {
                ModelState.AddModelError(nameof(model.Password), error.Description);
            }

            return await ActivatePageAsync(model);
        }

        // Following the link is the round trip that proves the address, so the user may now have what bought with it:
        // the order the account was made from now, earlier guest orders when they sign in.
        var orderId = user.Properties[PendingOrderPropertyName]?.GetValue<string>();

        user.Properties.Remove(PendingOrderPropertyName);
        UserEmailOwnership.MarkVerified(user.Properties, user.Email, _clock.UtcNow);
        await _userManager.UpdateAsync(user);

        if (!string.IsNullOrEmpty(orderId) &&
            await _orderStore.FindByIdAsync(orderId, HttpContext.RequestAborted) is { UserId: null } order &&
            string.Equals(order.CustomerEmail?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            order.UserId = user.UserId;
            await _orderStore.UpdateAsync(order, HttpContext.RequestAborted);
        }

        await _notifier.SuccessAsync(user.IsEnabled
            ? H["Your account is ready. Sign in with {0} and your new password.", user.Email]
            : H["Your account is ready. You can sign in once the store approves it."]);

        return LocalRedirect(Url.Content("~/"));
    }

    // The page is a shape, so a theme can replace it. The password is never sent back to the browser.
    private async Task<IActionResult> ActivatePageAsync(OrderAccountActivateViewModel model)
        => View("Activate", await _shapeFactory.CreateAsync<OrderAccountActivateViewModel>("OrderAccountActivate", shape =>
        {
            shape.UserId = model.UserId;
            shape.Code = model.Code;
        }));

    // A user made from an order that hasn't followed its link yet.
    private async Task<User> FindPendingAsync(string userId)
        => !string.IsNullOrEmpty(userId) &&
            await _userManager.FindByIdAsync(userId) is User { EmailConfirmed: false } user &&
            user.Properties[PendingOrderPropertyName] is not null
                ? user
                : null;
    // The account signs in with its email; its user name only has to be unique, and many sites allow letters and
    // digits only, so it is made from the email's first part.
    private async Task<string> NewUserNameAsync(string email)
    {
        var local = email.Split('@')[0];
        var name = new string([.. local.Where(char.IsAsciiLetterOrDigit)]).ToLowerInvariant();

        if (name.Length < 3)
        {
            name = "customer" + name;
        }

        var candidate = name;

        for (var attempt = 0; await _userManager.FindByNameAsync(candidate) is not null; attempt++)
        {
            candidate = name + Random.Shared.Next(100, attempt < 5 ? 1000 : 100000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private async Task<bool> SendActivationAsync(User user)
    {
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var url = Url.RouteUrl(ActivateRouteName, new { userId = user.UserId, code }, Request.Scheme);

        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("The account page couldn't be linked, so no email was sent to finish the new account.");

            return false;
        }

        var link = HtmlEncoder.Default.Encode(url);

        await _emailSender.SendAsync(new CommerceEmail
        {
            To = user.Email,
            Reference = user.UserId,
        }, _ => Task.FromResult(new CommerceEmailContent
        {
            Subject = S["Finish your account"],
            Text = S["Open this link to choose your password and finish your account: {0}", url],
            Html = $"<p>{HtmlEncoder.Default.Encode(S["Thanks for creating an account. Choose your password to finish it:"].Value)}</p><p><a href=\"{link}\">{HtmlEncoder.Default.Encode(S["Choose your password"].Value)}</a></p>",
        }));

        return true;
    }
}