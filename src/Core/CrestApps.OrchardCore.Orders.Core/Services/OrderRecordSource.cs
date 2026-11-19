using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Makes orders a record kind (<see cref="OrderConstants.RecordKind"/>), so any module can link to an order, pick one
/// and show its number without referencing Orders. The id is the order's item id. Users who manage orders may search
/// them; the search is the one the orders list runs: the start of an order number, a customer's email address or a
/// purchase order number.
/// </summary>
public sealed class OrderRecordSource : IRecordSource
{
    private const string DetailAction = "Detail";
    private const string DetailController = "OrdersAdmin";

    // With nothing typed, the picker shows the orders the list shows first: the ones placed.
    private static readonly OrderStatus[] _placedStatuses = [OrderStatus.Paid, OrderStatus.AwaitingPayment];

    private readonly IOrderStore _orderStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderRecordSource"/> class.
    /// </summary>
    /// <param name="orderStore">The order store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderRecordSource(
        IOrderStore orderStore,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<OrderRecordSource> stringLocalizer)
    {
        _orderStore = orderStore;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => OrderConstants.RecordKind;

    /// <inheritdoc/>
    public LocalizationSource DisplayName => LocalizationSource.Create<OrderRecordSource>("Order");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, OrderPermissions.ManageOrders);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        var text = search.Text?.Trim();

        var (_, orders) = await _orderStore.PageAsync(
            new OrderQuery
            {
                Search = text,
                Statuses = string.IsNullOrEmpty(text) ? _placedStatuses : [],
            },
            0,
            search.Count,
            cancellationToken);

        return [.. orders.Select(ToSummary)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return summaries;
        }

        foreach (var order in await _orderStore.GetAsync(wanted, cancellationToken))
        {
            summaries[order.ItemId] = ToSummary(order);
        }

        return summaries;
    }

    private RecordSummary ToSummary(Order order)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new RecordSummary
        {
            Id = order.ItemId,
            Title = order.OrderNumber,
            Description = StatusName(order.Status),
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, DetailAction, DetailController, new { area = OrderConstants.Features.Orders, orderId = order.ItemId }),
        };
    }

    private string StatusName(OrderStatus status)
        => status switch
        {
            OrderStatus.Draft => S["Draft"],
            OrderStatus.AwaitingPayment => S["Awaiting payment"],
            OrderStatus.Paid => S["Paid"],
            OrderStatus.Canceled => S["Canceled"],
            _ => status.ToString(),
        };
}
