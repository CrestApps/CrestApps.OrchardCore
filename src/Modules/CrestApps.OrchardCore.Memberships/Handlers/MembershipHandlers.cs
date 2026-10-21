using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Memberships.Handlers;

/// <summary>
/// Makes or extends a membership when its order is paid, and ends it when the order is canceled or refunded in full.
/// </summary>
internal sealed class MembershipOrderEventHandler : OrderEventHandlerBase
{
    private readonly MembershipService _memberships;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IUserService _userService;

    internal readonly IStringLocalizer S;

    public MembershipOrderEventHandler(
        MembershipService memberships,
        IHttpContextAccessor httpContextAccessor,
        SignInManager<IUser> signInManager,
        IUserService userService,
        IStringLocalizer<MembershipOrderEventHandler> stringLocalizer)
    {
        _memberships = memberships;
        _httpContextAccessor = httpContextAccessor;
        _signInManager = signInManager;
        _userService = userService;
        S = stringLocalizer;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        if (await _memberships.GrantAsync(context.Order, cancellationToken) == 0)
        {
            return;
        }

        // Roles are read from the sign-in, so a buyer paying now is signed in again to open what they bought at once.
        // Paid any other way, such as by a payment provider's webhook, the sign-in catches up when it's next checked.
        if (_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId &&
            userId == context.Order.UserId &&
            await _userService.GetUserByUniqueIdAsync(userId) is { } user)
        {
            await _signInManager.RefreshSignInAsync(user);
        }
    }

    public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        => _memberships.RevokeOrderAsync(context.Order, S["Its order was canceled."], cancellationToken);

    public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        => context.Order.RefundStatus == OrderRefundStatus.Refunded
            ? _memberships.RevokeOrderAsync(context.Order, S["Its order was refunded."], cancellationToken)
            : Task.CompletedTask;
}

/// <summary>
/// A membership is bought signed in: it puts an account in roles, and a guest has none.
/// </summary>
internal sealed class MembershipCartLineHandler : ICartLineHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public MembershipCartLineHandler(IHttpContextAccessor httpContextAccessor, IStringLocalizer<MembershipCartLineHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.Has<MembershipPart>() == true && !IsSignedIn())
        {
            context.Refuse(S["Sign in to buy {0}: a membership is for your account.", context.Title]);
        }

        return Task.CompletedTask;
    }

    public Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.Has<MembershipPart>() == true && !IsSignedIn())
        {
            context.Problem ??= S["Sign in to buy {0}: a membership is for your account.", context.Title];
        }

        return Task.CompletedTask;
    }

    private bool IsSignedIn()
        => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
