using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public class SubscriberDashboardDisplayDriver : DisplayDriver<SubscriberDashboard>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly ILocalClock _localClock;
    private readonly YesSql.ISession _session;

    public SubscriberDashboardDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        ILocalClock localClock,
        YesSql.ISession session)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _localClock = localClock;
        _session = session;
    }

    public override async Task<IDisplayResult> DisplayAsync(SubscriberDashboard model, BuildDisplayContext context)
    {
        var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User);

        if (user is not User u)
        {
            return null;
        }

        var userInfo = Initialize<SubscriberInfoViewModel>("SubscriberInfo", async vm =>
        {
            vm.UserId = u.UserId;
            vm.UserName = u.UserName;
            vm.Email = u.Email;
            vm.DisplayName = await _displayNameProvider.GetAsync(user);
        }).Location("Content:1");

        var invoices = Initialize<SubscriberInvoicesViewModel>("SubscriberInvoices", async vm =>
        {
            var user = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var items = await _session.QueryIndex<SubscriptionTransactionIndex>(index => index.OwnerId == user)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync();

            vm.Invoices = [];

            foreach (var item in items)
            {
                var invoice = new SubscriberInvoiceViewModel
                {
                    Amount = item.Amount,
                    Status = item.Status,
                    Date = (await _localClock.ConvertToLocalAsync(item.CreatedUtc)).DateTime,
                    Type = item.ContentType
                };

                vm.Invoices.Add(invoice);
            }

        }).Location("Content:5");


        return Combine(userInfo, invoices);
    }
}
