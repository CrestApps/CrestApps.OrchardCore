using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Shipping;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Fulfillment.Services;

/// <summary>
/// Tells the people who ship orders, through Orchard's notification center, when an order sent to a fulfillment
/// partner needs them. One notice per person each time a request becomes Needs attention, linking to the order.
/// </summary>
public sealed class FulfillmentNotificationHandler : IFulfillmentRequestHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FulfillmentRequestText _text;
    private readonly IEnumerable<IFulfillmentProvider> _providers;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentNotificationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the notification service, the roles and the users.</param>
    /// <param name="text">The sentences for request states.</param>
    /// <param name="providers">The fulfillment partners whose features are on, for their names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FulfillmentNotificationHandler(
        IServiceProvider serviceProvider,
        FulfillmentRequestText text,
        IEnumerable<IFulfillmentProvider> providers,
        IStringLocalizer<FulfillmentNotificationHandler> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _text = text;
        _providers = providers;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the order page a notice links to, application-relative.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The URL.</returns>
    public static string OrderUrl(FulfillmentRequest request)
        => "~/Admin/orders/" + request.OrderId;

    /// <inheritdoc/>
    public async Task NeedsAttentionAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var notifications = _serviceProvider.GetService<INotificationService>();
        var userManager = _serviceProvider.GetService<UserManager<IUser>>();

        if (notifications is null || userManager is null)
        {
            return;
        }

        var partner = _providers.FirstOrDefault(provider => provider.Name == request.ProviderName)?.DisplayName.Value ?? request.ProviderName;

        foreach (var user in await GetRecipientsAsync(userManager))
        {
            await notifications.SendAsync(user, new NotificationMessage
            {
                Subject = S["Order {0} needs attention", request.OrderNumber],
                Summary = S["Sending order {0} to {1} needs a person.", request.OrderNumber, partner],
                TextBody = _text.Reason(request),
                Url = OrderUrl(request),
                Type = FulfillmentConstants.NeedsAttentionNotificationType,
                ReferenceId = request.ItemId,
            }, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<IUser>> GetRecipientsAsync(UserManager<IUser> userManager)
    {
        // The people allowed to ship orders: administrators, and every role granted the permission.
        var permission = ShippingPermissions.FulfillOrders;
        var names = new HashSet<string>(StringComparer.Ordinal) { permission.Name };

        foreach (var implying in permission.ImpliedBy ?? [])
        {
            names.Add(implying.Name);
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { OrchardCoreConstants.Roles.Administrator };

        if (_serviceProvider.GetService<IRoleService>() is { } roleService)
        {
            foreach (var role in await roleService.GetRolesAsync())
            {
                if (role is Role editable && editable.RoleClaims?.Any(claim => claim.ClaimType == Permission.ClaimType && names.Contains(claim.ClaimValue)) == true)
                {
                    roles.Add(role.RoleName);
                }
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var users = new List<IUser>();

        foreach (var role in roles)
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(role))
            {
                if (await userManager.GetUserIdAsync(user) is { Length: > 0 } id && ids.Add(id))
                {
                    users.Add(user);
                }
            }
        }

        return users;
    }
}
