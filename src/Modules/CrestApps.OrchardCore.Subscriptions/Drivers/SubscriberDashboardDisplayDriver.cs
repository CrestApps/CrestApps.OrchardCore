using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
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
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly YesSql.ISession _session;

    public SubscriberDashboardDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IContentManager contentManager,
        IClock clock,
        ILocalClock localClock,
        YesSql.ISession session)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _contentManager = contentManager;
        _clock = clock;
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

        var subscriptions = Initialize<ListSubscriptionSummariesViewModel>("ListSubscriptions", async vm =>
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sessions = await _session.Query<SubscriptionSession, SubscriptionIndex>(index => index.OwnerId == userId)
            .OrderByDescending(x => x.StartedAt)
            .ListAsync();

            vm.Subscriptions = [];
            var now = _clock.UtcNow;
            foreach (var session in sessions)
            {
                var metadata = session.As<SubscriptionsMetadata>();

                if (metadata?.Subscriptions == null || metadata.Subscriptions.Count == 0)
                {
                    continue;
                }

                var contentItem = await _contentManager.GetVersionAsync(session.ContentItemVersionId);

                foreach (var subscription in metadata.Subscriptions)
                {
                    var summaryModel = new SubscriptionsSummaryViewModel
                    {
                        StartedAt = (await _localClock.ConvertToLocalAsync(subscription.StartedAt)).DateTime,
                        ServicePlanTitle = contentItem.DisplayText,
                        SessionId = session.SessionId,
                        IsActive = true,
                    };

                    if (subscription.ExpiresAt.HasValue)
                    {
                        summaryModel.ExpiresAt = (await _localClock.ConvertToLocalAsync(subscription.ExpiresAt.Value)).DateTime;
                        summaryModel.IsActive = subscription.ExpiresAt.Value > now;
                    }

                    vm.Subscriptions.Add(summaryModel);
                }
            }
        }).Location("Content:5");


        var invoices = Initialize<SubscriberInvoicesViewModel>("SubscriberInvoices", async vm =>
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var items = await _session.QueryIndex<SubscriptionTransactionIndex>(index => index.OwnerId == userId)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync();

            vm.Invoices = [];

            foreach (var item in items)
            {
                var contentItem = await _contentManager.GetVersionAsync(item.ContentItemVersionId);

                var invoice = new SubscriberInvoiceViewModel
                {
                    Amount = item.Amount,
                    Status = item.Status,
                    Date = (await _localClock.ConvertToLocalAsync(item.CreatedUtc)).DateTime,
                    ServicePlanTitle = contentItem.DisplayText
                };

                vm.Invoices.Add(invoice);
            }
        }).Location("Content:10");

        return Combine(userInfo, subscriptions, invoices);
    }
}
