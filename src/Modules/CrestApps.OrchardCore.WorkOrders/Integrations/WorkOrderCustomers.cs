using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// The customers, companies and CRM contacts a work order can be for, found through the asset owner sources that
/// Customers, Companies and the CRM register when Assets is on.
/// </summary>
public sealed class WorkOrderCustomers
{
    private readonly AssetOwnerDirectory _owners;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderCustomers"/> class.
    /// </summary>
    /// <param name="sources">The owner sources.</param>
    public WorkOrderCustomers(IEnumerable<IAssetOwnerSource> sources)
    {
        _owners = new AssetOwnerDirectory(sources);
    }

    /// <summary>
    /// Gets whether a kind of customer can be picked.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Whether it can.</returns>
    public bool CanPick(AssetOwnerKind kind)
        => _owners.GetSource(kind) is not null;

    /// <summary>
    /// Gets a customer's name, or <see langword="null"/>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The name.</returns>
    public Task<string> NameAsync(AssetOwnerKind kind, string id, CancellationToken cancellationToken = default)
        => _owners.GetNameAsync(kind, id, cancellationToken);

    /// <summary>
    /// Finds customers of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="query">The text typed.</param>
    /// <param name="count">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The customers found.</returns>
    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(AssetOwnerKind kind, string query, int count, CancellationToken cancellationToken = default)
        => _owners.GetSource(kind) is { } source ? await source.SearchAsync(query, count, cancellationToken) : [];
}

/// <summary>
/// The search behind the work order editor's customer, company and contact pickers.
/// </summary>
internal static class WorkOrderCustomerEndpoints
{
    private const int MaxResults = 50;

    /// <summary>
    /// Maps the search endpoint.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder AddWorkOrderCustomerEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/work-orders/customers/search", (Delegate)SearchAsync)
            .RequireAuthorization()
            .WithName("CrestApps.WorkOrders.CustomerSearch");

        return builder;
    }

    internal static async Task<IResult> SearchAsync(AssetOwnerKind kind, string query, HttpContext httpContext, IAuthorizationService authorizationService, WorkOrderCustomers customers)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Results.Forbid();
        }

        var found = await customers.SearchAsync(kind, query, MaxResults, httpContext.RequestAborted);

        return Results.Ok(found.Select(owner => new { value = owner.Id, text = owner.Name }));
    }
}
