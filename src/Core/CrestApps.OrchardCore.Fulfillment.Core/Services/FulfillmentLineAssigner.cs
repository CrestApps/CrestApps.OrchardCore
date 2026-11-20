using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Assigns an order's lines to fulfillment partners: asks every connected partner which lines it makes, gives a line to
/// at most one partner, and caps each claim to the quantity left to ship.
/// </summary>
public sealed class FulfillmentLineAssigner
{
    private readonly IEnumerable<IFulfillmentProvider> _providers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentLineAssigner"/> class.
    /// </summary>
    /// <param name="providers">The fulfillment partners whose features are on.</param>
    /// <param name="logger">The logger.</param>
    public FulfillmentLineAssigner(IEnumerable<IFulfillmentProvider> providers, ILogger<FulfillmentLineAssigner> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Works out the lines assigned to one partner.
    /// </summary>
    /// <param name="provider">The partner.</param>
    /// <param name="order">The order, with every physical line left to ship.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The quantity assigned to the partner for each line, or the line another partner also claims.</returns>
    public async Task<FulfillmentAssignment> AssignAsync(IFulfillmentProvider provider, FulfillmentOrder order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(order);

        var left = order.Lines.ToDictionary(line => line.LineId, line => line.Quantity, StringComparer.Ordinal);
        var claimants = new Dictionary<string, List<IFulfillmentProvider>>(StringComparer.Ordinal);
        var own = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var candidate in _providers)
        {
            var isTarget = string.Equals(candidate.Name, provider.Name, StringComparison.Ordinal);

            if (!isTarget && !await candidate.IsConnectedAsync())
            {
                continue;
            }

            IReadOnlyList<FulfillmentLineClaim> claims;

            try
            {
                claims = await candidate.ClaimLinesAsync(order, cancellationToken);
            }
            catch (Exception exception) when (!isTarget && exception is not OperationCanceledException)
            {
                // Another partner's failure must not stop this one; its lines simply stay unclaimed by it.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(exception, "The fulfillment partner {Provider} could not claim the lines of order '{OrderNumber}'.", candidate.Name, order.OrderNumber);
                }

                continue;
            }

            foreach (var claim in claims ?? [])
            {
                if (claim is null || claim.Quantity <= 0 || !left.TryGetValue(claim.LineId, out var available))
                {
                    continue;
                }

                if (!claimants.TryGetValue(claim.LineId, out var partners))
                {
                    partners = [];
                    claimants[claim.LineId] = partners;
                }

                if (!partners.Contains(candidate))
                {
                    partners.Add(candidate);
                }

                if (isTarget)
                {
                    // A claim larger than what is left is capped, so units are never sent twice.
                    own[claim.LineId] = Math.Min(available, own.GetValueOrDefault(claim.LineId) + claim.Quantity);
                }
            }
        }

        foreach (var lineId in own.Keys)
        {
            var partners = claimants[lineId];

            if (partners.Count > 1)
            {
                var title = order.Lines.First(line => line.LineId == lineId).Title;

                return FulfillmentAssignment.Conflict(title, partners.Select(partner => partner.DisplayName.Value).ToList());
            }
        }

        return FulfillmentAssignment.Assigned(own);
    }
}
