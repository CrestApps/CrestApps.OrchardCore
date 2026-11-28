using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// What the return tools share: finding a return by its id or its number, and the names the tools give its statuses,
/// which are the filter names of Commerce › Returns (a rejected return is "declined", a canceled one "withdrawn").
/// </summary>
internal static class ReturnToolSupport
{
    /// <summary>
    /// The status names the tools take and return.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, OrderReturnStatus> Statuses = new Dictionary<string, OrderReturnStatus>(StringComparer.OrdinalIgnoreCase)
    {
        ["requested"] = OrderReturnStatus.Requested,
        ["approved"] = OrderReturnStatus.Approved,
        ["received"] = OrderReturnStatus.Received,
        ["completed"] = OrderReturnStatus.Completed,
        ["declined"] = OrderReturnStatus.Rejected,
        ["withdrawn"] = OrderReturnStatus.Canceled,
    };

    /// <summary>
    /// Gets the name the tools give a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The name, such as <c>declined</c>.</returns>
    public static string NameOf(OrderReturnStatus status)
        => status switch
        {
            OrderReturnStatus.Requested => "requested",
            OrderReturnStatus.Approved => "approved",
            OrderReturnStatus.Received => "received",
            OrderReturnStatus.Completed => "completed",
            OrderReturnStatus.Rejected => "declined",
            OrderReturnStatus.Canceled => "withdrawn",
            _ => status.ToString(),
        };

    /// <summary>
    /// Finds a return by its id or its number.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="returnId">The return's id, or <see langword="null"/>.</param>
    /// <param name="returnNumber">The return's number, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The return, or the sentence that says why there is none.</returns>
    public static async Task<(OrderReturn Return, string Error)> FindAsync(IServiceProvider services, string returnId, string returnNumber, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IOrderReturnStore>();

        if (returnId is not null)
        {
            return await store.FindByIdAsync(returnId, cancellationToken) is { } byId
                ? (byId, null)
                : (null, $"Nothing was done: no return has the id '{returnId}'. Use listOrderReturns to find it.");
        }

        if (returnNumber is not null)
        {
            var (_, page) = await store.PageAsync(new OrderReturnQuery { Search = returnNumber, All = true }, 0, 10, cancellationToken);

            return page.FirstOrDefault(entry => string.Equals(entry.Number, returnNumber, StringComparison.OrdinalIgnoreCase)) is { } byNumber
                ? (byNumber, null)
                : (null, $"Nothing was done: no return has the number '{returnNumber}'. Use listOrderReturns to find it.");
        }

        return (null, "Nothing was done: give the return's id or its number. Use listOrderReturns to find it.");
    }
}
