using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// What the estimate and recurring plan AI tools share: finding an estimate by the number the customer was given or by
/// its id, and the admin pages they link to.
/// </summary>
internal static class EstimateToolSupport
{
    /// <summary>
    /// Finds an estimate by its number, such as EST-1001, or by its id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="key">The number or the id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The estimate, or <see langword="null"/> when none has that number or id.</returns>
    public static async Task<Estimate> FindAsync(IServiceProvider services, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return await services.GetRequiredService<IEstimateStore>().FindByNumberAsync(key, cancellationToken)
            ?? await services.GetRequiredService<IEstimateManager>().FindByIdAsync(key, cancellationToken);
    }

    /// <summary>
    /// Gets the path of a record's Invoicing admin page, or <see langword="null"/> when there is no request to build it
    /// from.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="controller">The controller, such as <c>EstimatesAdmin</c>.</param>
    /// <param name="itemId">The record's id.</param>
    /// <returns>The path.</returns>
    public static string AdminPath(IServiceProvider services, string controller, string itemId)
        => string.IsNullOrEmpty(itemId)
            ? null
            : AIModuleTools.GetAdminPath(services, InvoicingConstants.Features.Area, controller, "Detail", new RouteValueDictionary { ["itemId"] = itemId });
}
