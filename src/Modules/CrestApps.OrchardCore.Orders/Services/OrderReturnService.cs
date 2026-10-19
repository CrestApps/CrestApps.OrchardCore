using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Returns, from the buyer's request to the store closing them: approved or declined, received (with what goes back
/// into stock, through the orders' event handlers), then refunded or exchanged. Each step is noted on the order and
/// emailed to the buyer.
/// </summary>
public sealed class OrderReturnService
{
    private const int MaxCommentLength = 2000;

    private readonly IOrderReturnStore _returns;
    private readonly IOrderStore _orders;
    private readonly IOrderService _orderService;
    private readonly IEnumerable<IOrderEventHandler> _handlers;
    private readonly OrderOperationsService _operations;
    private readonly OrderReturnEmailSender _emails;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OrderReturnService(
        IOrderReturnStore returns,
        IOrderStore orders,
        IOrderService orderService,
        IEnumerable<IOrderEventHandler> handlers,
        OrderOperationsService operations,
        OrderReturnEmailSender emails,
        ISiteService siteService,
        IClock clock,
        IStringLocalizer<OrderReturnService> stringLocalizer)
    {
        _returns = returns;
        _orders = orders;
        _orderService = orderService;
        _handlers = handlers;
        _operations = operations;
        _emails = emails;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets what can still be returned of the order, by line id, or nothing when the window closed.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> GetReturnableAsync(Order order, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<ReturnSettings>();

        return OrderReturnPolicy.IsOpen(order, settings, _clock.UtcNow)
            ? OrderReturnPolicy.GetReturnable(order, await _returns.ListByOrderAsync(order.ItemId, cancellationToken))
            : new Dictionary<string, int>();
    }

    /// <summary>
    /// Records the buyer's request. Returns the return, or the problems for the buyer.
    /// </summary>
    public async Task<(OrderReturn Return, IReadOnlyList<string> Problems)> RequestAsync(Order order, OrderReturnRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(request);

        var settings = await _siteService.GetSettingsAsync<ReturnSettings>();
        var returnable = await GetReturnableAsync(order, cancellationToken);
        var problems = new List<string>();
        var lines = new List<OrderReturnLine>();

        if (returnable.Count == 0)
        {
            problems.Add(S["This order can no longer be returned online. Contact us and we'll help."]);

            return (null, problems);
        }

        foreach (var (lineId, quantity, reason) in request.Lines.Where(line => line.Quantity > 0))
        {
            var line = order.Lines.FirstOrDefault(entry => entry.LineId == lineId);

            if (line is null || !returnable.TryGetValue(lineId, out var left))
            {
                continue;
            }

            if (quantity > left)
            {
                problems.Add(S["Only {0} of {1} can be returned.", left, line.Title]);

                continue;
            }

            if (string.IsNullOrWhiteSpace(reason) || !(settings.Reasons ?? []).Contains(reason, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add(S["Choose why you're returning {0}.", line.Title]);

                continue;
            }

            lines.Add(new OrderReturnLine
            {
                LineId = line.LineId,
                Title = OrderLineText.Describe(line),
                Sku = line.Sku,
                Quantity = quantity,
                UnitPrice = OrderReturnPolicy.UnitPriceOf(line),
                Reason = reason.Trim(),
            });
        }

        if (problems.Count == 0 && lines.Count == 0)
        {
            problems.Add(S["Choose how many of an item to return."]);
        }

        var resolution = settings.AllowExchanges ? request.Resolution : OrderReturnResolution.Refund;

        if (resolution == OrderReturnResolution.Exchange && string.IsNullOrWhiteSpace(request.ExchangeFor))
        {
            problems.Add(S["Tell us what you'd like instead, such as another size or color."]);
        }

        if (problems.Count > 0)
        {
            return (null, problems);
        }

        var now = _clock.UtcNow;
        var orderReturn = new OrderReturn
        {
            ItemId = UniqueId.GenerateId(),
            Number = await NewNumberAsync(now, cancellationToken),
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Currency = order.Currency,
            Lines = lines,
            Resolution = resolution,
            ExchangeFor = resolution == OrderReturnResolution.Exchange ? Trim(request.ExchangeFor, 500) : null,
            Comment = Trim(request.Comment, MaxCommentLength),
            Status = OrderReturnStatus.Requested,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await _returns.CreateAsync(orderReturn, cancellationToken);
        await _orderService.AddNoteAsync(order, S["Return {0} requested: {1}.", orderReturn.Number, Describe(orderReturn)], OrderNoteKind.Event, order.CustomerName, cancellationToken);
        await _emails.SendAsync(orderReturn, OrderReturnEmailKind.Requested, cancellationToken);

        return (orderReturn, []);
    }

    /// <summary>
    /// Withdraws a request the store hasn't answered yet.
    /// </summary>
    public async Task<bool> WithdrawAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Status != OrderReturnStatus.Requested)
        {
            return false;
        }

        await CloseAsync(orderReturn, OrderReturnStatus.Canceled, S["Return {0} withdrawn by the buyer.", orderReturn.Number], actorName: orderReturn.CustomerName, cancellationToken);

        return true;
    }

    public async Task<bool> ApproveAsync(OrderReturn orderReturn, string message, string actorName, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Status != OrderReturnStatus.Requested)
        {
            return false;
        }

        var now = _clock.UtcNow;

        orderReturn.Status = OrderReturnStatus.Approved;
        orderReturn.StoreMessage = Trim(message, MaxCommentLength);
        orderReturn.ApprovedUtc = orderReturn.ModifiedUtc = now;

        await _returns.UpdateAsync(orderReturn, cancellationToken);
        await NoteAsync(orderReturn, S["Return {0} approved.", orderReturn.Number], actorName, cancellationToken);
        await _emails.SendAsync(orderReturn, OrderReturnEmailKind.Approved, cancellationToken);

        return true;
    }

    public async Task<bool> RejectAsync(OrderReturn orderReturn, string message, string actorName, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Status is not (OrderReturnStatus.Requested or OrderReturnStatus.Approved) || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        orderReturn.StoreMessage = Trim(message, MaxCommentLength);

        await CloseAsync(orderReturn, OrderReturnStatus.Rejected, S["Return {0} declined: {1}", orderReturn.Number, orderReturn.StoreMessage], actorName, cancellationToken);
        await _emails.SendAsync(orderReturn, OrderReturnEmailKind.Rejected, cancellationToken);

        return true;
    }

    /// <summary>
    /// Records that the items came back. The lines named in <paramref name="restockLineIds"/> go back into stock, at
    /// <paramref name="locationId"/> or where they were sold from.
    /// </summary>
    public async Task<bool> ReceiveAsync(OrderReturn orderReturn, IReadOnlyCollection<string> restockLineIds, string locationId, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Status is not (OrderReturnStatus.Requested or OrderReturnStatus.Approved) ||
            await _orders.FindByIdAsync(orderReturn.OrderId, cancellationToken) is not { } order)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var returned = new List<OrderReturnedLine>();

        foreach (var line in orderReturn.Lines)
        {
            line.Restocked = restockLineIds?.Contains(line.LineId) == true;

            if (order.Lines.FirstOrDefault(entry => entry.LineId == line.LineId) is { } orderLine)
            {
                returned.Add(new OrderReturnedLine(orderLine, line.Quantity, line.Restocked));
            }
        }

        orderReturn.Status = OrderReturnStatus.Received;
        orderReturn.RestockLocationId = string.IsNullOrWhiteSpace(locationId) ? null : locationId;
        orderReturn.ApprovedUtc ??= now;
        orderReturn.ReceivedUtc = orderReturn.ModifiedUtc = now;

        var context = new OrderReturnedContext(order, orderReturn, returned)
        {
            LocationId = orderReturn.RestockLocationId,
            ActorId = actorId,
            ActorName = actorName,
        };

        foreach (var handler in _handlers)
        {
            await handler.ReturnedAsync(context, cancellationToken);
        }

        await _returns.UpdateAsync(orderReturn, cancellationToken);

        var restocked = orderReturn.Lines.Where(line => line.Restocked).Sum(line => line.Quantity);

        await NoteAsync(orderReturn, restocked > 0
            ? S["Return {0} received; {1} back in stock.", orderReturn.Number, restocked]
            : S["Return {0} received; nothing back in stock.", orderReturn.Number], actorName, cancellationToken);

        return true;
    }

    /// <summary>
    /// Gives the buyer <paramref name="amount"/> back for the return and closes it. Returns the problems, if any.
    /// </summary>
    public async Task<IReadOnlyList<string>> RefundAsync(OrderReturn orderReturn, decimal amount, string actorName, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Status is not (OrderReturnStatus.Approved or OrderReturnStatus.Received) ||
            await _orders.FindByIdAsync(orderReturn.OrderId, cancellationToken) is not { } order)
        {
            return [S["This return can't be refunded now."]];
        }

        var result = await _operations.RefundAsync(order, amount, S["Return {0}", orderReturn.Number], restock: false, actorName, cancellationToken);

        if (result.Errors.Count > 0)
        {
            return [.. result.Errors];
        }

        orderReturn.RefundedAmount += result.Refunded;

        await CloseAsync(orderReturn, OrderReturnStatus.Completed, S["Return {0} refunded: {1}.", orderReturn.Number, ProductPriceFormatter.Format(result.Refunded, order.Currency)], actorName, cancellationToken);
        await _emails.SendAsync(orderReturn, OrderReturnEmailKind.Refunded, cancellationToken);

        return [];
    }

    /// <summary>
    /// Closes an exchange once the replacement is on its way.
    /// </summary>
    public async Task<bool> CompleteExchangeAsync(OrderReturn orderReturn, string message, string actorName, CancellationToken cancellationToken = default)
    {
        if (orderReturn.Resolution != OrderReturnResolution.Exchange || orderReturn.Status is not (OrderReturnStatus.Approved or OrderReturnStatus.Received))
        {
            return false;
        }

        orderReturn.StoreMessage = Trim(message, MaxCommentLength) ?? orderReturn.StoreMessage;

        await CloseAsync(orderReturn, OrderReturnStatus.Completed, S["Return {0} exchanged.", orderReturn.Number], actorName, cancellationToken);
        await _emails.SendAsync(orderReturn, OrderReturnEmailKind.Exchanged, cancellationToken);

        return true;
    }

    private async Task CloseAsync(OrderReturn orderReturn, OrderReturnStatus status, string note, string actorName, CancellationToken cancellationToken)
    {
        orderReturn.Status = status;
        orderReturn.ClosedUtc = orderReturn.ModifiedUtc = _clock.UtcNow;

        await _returns.UpdateAsync(orderReturn, cancellationToken);
        await NoteAsync(orderReturn, note, actorName, cancellationToken);
    }

    private async Task NoteAsync(OrderReturn orderReturn, string note, string actorName, CancellationToken cancellationToken)
    {
        if (await _orders.FindByIdAsync(orderReturn.OrderId, cancellationToken) is { } order)
        {
            await _orderService.AddNoteAsync(order, note, OrderNoteKind.Event, actorName, cancellationToken);
        }
    }

    private static string Describe(OrderReturn orderReturn)
        => string.Join(", ", orderReturn.Lines.Select(line => $"{line.Quantity} × {line.Title} ({line.Reason})"));

    private static string Trim(string value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim() is { } trimmed && trimmed.Length > maxLength ? trimmed[..maxLength] : value.Trim();

    // A number the buyer can read out: the date and four characters, such as R-261019-K3QZ.
    private async Task<string> NewNumberAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (var attempt = 0; ; attempt++)
        {
            var suffix = string.Concat(Enumerable.Range(0, 4).Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]));
            var number = $"R-{now.ToString("yyMMdd", CultureInfo.InvariantCulture)}-{suffix}";

            var (count, _) = await _returns.PageAsync(new OrderReturnQuery { Search = number, All = true }, 0, 1, cancellationToken);

            if (count == 0 || attempt >= 5)
            {
                return number;
            }
        }
    }
}

/// <summary>
/// What the buyer asked for: units of lines with a reason each, and their money back or something else.
/// </summary>
public sealed class OrderReturnRequest
{
    public IReadOnlyList<(string LineId, int Quantity, string Reason)> Lines { get; init; } = [];

    public OrderReturnResolution Resolution { get; init; }

    public string ExchangeFor { get; init; }

    public string Comment { get; init; }
}
