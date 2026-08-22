using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays subscriber profile, subscription, and invoice information on the subscriber dashboard.
/// </summary>
public class SubscriberDashboardDisplayDriver : DisplayDriver<SubscriberDashboard>
{
    private const string _subscriptionsPageKey = "subscriptionsPage";
    private const string _invoicesPageKey = "invoicesPage";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentManager _contentManager;
    private readonly IShapeFactory _shapeFactory;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly global::YesSql.ISession _session;
    private readonly PagerOptions _pagerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriberDashboardDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to read the current subscriber from the HTTP context.</param>
    /// <param name="userManager">The user manager used to load the current Orchard Core user.</param>
    /// <param name="displayNameProvider">The display name provider used to format the subscriber display name.</param>
    /// <param name="contentManager">The content manager used to load service plan content item versions.</param>
    /// <param name="shapeFactory">The shape factory used to build the subscription and invoice pagers.</param>
    /// <param name="clock">The clock used to compare subscription expiration dates.</param>
    /// <param name="localClock">The local clock used to convert subscription and invoice dates to local time.</param>
    /// <param name="session">The YesSql session used to query subscription sessions and transactions.</param>
    /// <param name="pagerOptions">The pager options used to determine the subscriber dashboard page size.</param>
    public SubscriberDashboardDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IContentManager contentManager,
        IShapeFactory shapeFactory,
        IClock clock,
        ILocalClock localClock,
        global::YesSql.ISession session,
        IOptions<PagerOptions> pagerOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _contentManager = contentManager;
        _shapeFactory = shapeFactory;
        _clock = clock;
        _localClock = localClock;
        _session = session;
        _pagerOptions = pagerOptions.Value;
    }

    /// <summary>
    /// Builds the subscriber dashboard with subscriber details, subscription summaries, and invoices for the current user.
    /// </summary>
    /// <param name="model">The subscriber dashboard model to display.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The display result that renders the dashboard, or <see langword="null"/> when the current user cannot be loaded.</returns>
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

            var summaries = new List<SubscriptionsSummaryViewModel>();
            var now = _clock.UtcNow;
            foreach (var session in sessions)
            {
                if (!session.TryGet<SubscriptionsMetadata>(out var metadata) ||
                    metadata?.Subscriptions == null || metadata.Subscriptions.Count == 0)
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

                    summaries.Add(summaryModel);
                }
            }

            var orderedSummaries = summaries
            .OrderByDescending(x => x.StartedAt)
            .ToList();

            var pageSize = _pagerOptions.GetPageSize();
            var page = GetPageNumber(_subscriptionsPageKey);

            vm.Subscriptions = orderedSummaries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

            vm.Pager = await BuildPagerAsync(_subscriptionsPageKey, page, pageSize, orderedSummaries.Count, _invoicesPageKey);
        }).Location("Content:5");

        var invoices = Initialize<SubscriberInvoicesViewModel>("SubscriberInvoices", async vm =>
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var pageSize = _pagerOptions.GetPageSize();
            var page = GetPageNumber(_invoicesPageKey);

            var query = _session.QueryIndex<SubscriptionTransactionIndex>(index => index.OwnerId == userId);

            var totalCount = await query.CountAsync();

            var items = await query
            .OrderByDescending(x => x.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                    ServicePlanTitle = contentItem.DisplayText,
                    SessionId = item.SessionId,
                    TransactionId = item.GatewayTransactionId,
                };

                vm.Invoices.Add(invoice);
            }

            vm.Pager = await BuildPagerAsync(_invoicesPageKey, page, pageSize, totalCount, _subscriptionsPageKey);
        }).Location("Content:10");

        return Combine(userInfo, subscriptions, invoices);
    }

    private int GetPageNumber(string key)
    {
        if (int.TryParse(_httpContextAccessor.HttpContext.Request.Query[key], out var page) && page > 0)
        {
            return page;
        }

        return 1;
    }

    private async Task<IShape> BuildPagerAsync(
        string pagerId,
        int page,
        int pageSize,
        int totalItemCount,
        string companionPageKey)
    {
        var pager = new Pager(new PagerParameters { Page = page }, pageSize);

        var routeData = new RouteData();

        var companionPage = GetPageNumber(companionPageKey);

        if (companionPage > 1)
        {
            routeData.Values[companionPageKey] = companionPage;
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, totalItemCount, routeData);
        pagerShape.Properties["PagerId"] = pagerId;

        return pagerShape;
    }
}
