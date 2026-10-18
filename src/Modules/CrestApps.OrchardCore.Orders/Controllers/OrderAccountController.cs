using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout.Services;
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
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Email;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Makes an account for a guest from their order's confirmation: they choose a password, the account takes the
/// order's email, and the order joins the account. Earlier guest orders with the email join once it is confirmed.
/// </summary>
public sealed class OrderAccountController : Controller
{
    private readonly IOrderStore _orderStore;
    private readonly OrderAccountClaims _claims;
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public OrderAccountController(
        IOrderStore orderStore,
        OrderAccountClaims claims,
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        IPaymentAttemptLimiter limiter,
        INotifier notifier,
        ILogger<OrderAccountController> logger,
        IStringLocalizer<OrderAccountController> stringLocalizer,
        IHtmlLocalizer<OrderAccountController> htmlLocalizer)
    {
        _orderStore = orderStore;
        _claims = claims;
        _userManager = userManager;
        _signInManager = signInManager;
        _limiter = limiter;
        _notifier = notifier;
        _logger = logger;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [HttpPost("orders/create-account", Name = "OrderCreateAccount")]
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

        if (!await _limiter.AcquireAsync("order-create-account", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
        {
            await _notifier.ErrorAsync(H["Too many attempts. Wait a few minutes, then try again."]);

            return LocalRedirect(back);
        }

        if (string.IsNullOrEmpty(model.Password) || !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            await _notifier.ErrorAsync(H["The two passwords don't match."]);

            return LocalRedirect(back);
        }

        var email = order.CustomerEmail.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            await _notifier.InformationAsync(H["There's already an account with {0}. Sign in, and your orders with this email appear in your account.", email]);

            return LocalRedirect(back);
        }

        var registration = HttpContext.RequestServices.GetService<IOptions<RegistrationOptions>>()?.Value;
        var mustConfirm = registration?.UsersMustValidateEmail == true;
        var user = new User
        {
            UserName = await NewUserNameAsync(email),
            Email = email,
            EmailConfirmed = false,
            IsEnabled = registration?.UsersAreModerated != true,
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["Your account wasn't created: {0}", string.Join(" ", result.Errors.Select(error => error.Description))]);

            return LocalRedirect(back);
        }

        // The order this browser just placed is the customer's; earlier guest orders with the email join when it's
        // confirmed.
        order.UserId = user.UserId;
        await _orderStore.UpdateAsync(order, HttpContext.RequestAborted);

        if (!user.IsEnabled)
        {
            await _notifier.SuccessAsync(H["Your account was created. You can sign in once the store approves it."]);

            return LocalRedirect(back);
        }

        if (mustConfirm)
        {
            await SendConfirmationAsync(user);
            await _notifier.SuccessAsync(H["Your account was created. We sent a link to {0}; open it to confirm your email, then sign in.", email]);

            return LocalRedirect(back);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _notifier.SuccessAsync(H["Your account was created, and this order is in it."]);

        return RedirectToRoute("AccountOrder", new { orderId = order.ItemId });
    }

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

    private async Task SendConfirmationAsync(User user)
    {
        if (HttpContext.RequestServices.GetService<IEmailService>() is not { } emailService)
        {
            return;
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var url = Url.Action("ConfirmEmail", "EmailConfirmation", new { area = "OrchardCore.Users", userId = user.UserId, code }, Request.Scheme);

        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("The email confirmation page couldn't be linked, so no confirmation email was sent to the new account.");

            return;
        }

        var link = HtmlEncoder.Default.Encode(url);

        await emailService.SendAsync(new MailMessage
        {
            To = user.Email,
            Subject = S["Confirm your email"],
            TextBody = S["Open this link to confirm your email: {0}", url],
            HtmlBody = $"<p>{HtmlEncoder.Default.Encode(S["Thanks for creating an account. Confirm your email to sign in:"].Value)}</p><p><a href=\"{link}\">{HtmlEncoder.Default.Encode(S["Confirm your email"].Value)}</a></p>",
        });
    }
}
