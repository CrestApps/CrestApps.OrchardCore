using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Security;
using OrchardCore.Settings;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Tells the users in the roles chosen under Settings > Commerce > Inventory when a product runs low or sells out.
/// </summary>
internal sealed class LowStockNotificationHandler : IStockLevelHandler
{
    private const int MaxRecipients = 100;

    private readonly ISiteService _siteService;
    private readonly ISession _session;
    private readonly RoleManager<IRole> _roleManager;
    private readonly INotificationService _notificationService;

    internal readonly IStringLocalizer S;

    public LowStockNotificationHandler(
        ISiteService siteService,
        ISession session,
        RoleManager<IRole> roleManager,
        INotificationService notificationService,
        IStringLocalizer<LowStockNotificationHandler> stringLocalizer)
    {
        _siteService = siteService;
        _session = session;
        _roleManager = roleManager;
        _notificationService = notificationService;
        S = stringLocalizer;
    }

    public async Task ChangedAsync(StockLevelChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.RanLow)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<InventorySettings>();
        var roles = (settings.LowStockNotificationRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(_roleManager.NormalizeKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (roles.Length == 0)
        {
            return;
        }

        var users = await _session.Query<User>()
            .With<UserByRoleNameIndex>(index => index.RoleName.IsIn(roles))
            .With<UserIndex>(index => index.IsEnabled)
            .Take(MaxRecipients)
            .ListAsync(cancellationToken);

        var item = context.Item;
        var name = string.IsNullOrEmpty(item.Sku) ? item.Title : S["{0} (SKU {1})", item.Title, item.Sku].Value;
        var message = context.Level == StockLevel.Out
            ? new NotificationMessage
            {
                Subject = S["Out of stock: {0}", item.Title],
                Summary = S["{0} is out of stock.", item.Title],
                TextBody = item.AllowBackorder
                    ? S["{0} has no units left. It still takes orders as backorders. Receive more under Commerce > Inventory.", name]
                    : S["{0} has no units left, so shoppers can't buy it. Receive more under Commerce > Inventory.", name],
            }
            : new NotificationMessage
            {
                Subject = S["Low stock: {0}", item.Title],
                Summary = S["Only {0} of {1} left.", item.Available, item.Title],
                TextBody = S["{0} is down to {1} available, at or below its low-stock threshold of {2}. Receive more under Commerce > Inventory.", name, item.Available, item.LowStockThreshold],
            };

        foreach (var user in users)
        {
            await _notificationService.SendAsync(user, message, cancellationToken);
        }
    }
}
