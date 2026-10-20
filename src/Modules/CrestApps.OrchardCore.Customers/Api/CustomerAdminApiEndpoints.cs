using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Customers.Api;

/// <summary>
/// The store's customers over the API, for staff and back-office systems with View customers.
/// </summary>
internal static class CustomerAdminApiEndpoints
{
    public const string Tag = "Customers";

    public static IEndpointRouteBuilder MapCustomerAdminApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("customers", ListAsync)
            .WithName("ListCustomers")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the store's customers: searched by name, email or phone, by kind, and whether they have an account.");

        group.MapGet("customers/{customerId}", GetAsync)
            .WithName("GetCustomer")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a customer with their addresses.");

        return routes;
    }

    private static async Task<Results<Ok<ApiPage<ApiCustomer>>, ProblemHttpResult>> ListAsync(
        string q,
        string kind,
        bool? hasAccount,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICustomerStore customers)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CustomerPermissions.ViewCustomers))
        {
            return ApiProblems.Forbidden();
        }

        var pageSkip = ApiPage<ApiCustomer>.ClampSkip(skip);
        var pageTake = ApiPage<ApiCustomer>.ClampTake(take);
        var (count, page) = await customers.PageAsync(new CustomerQuery
        {
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Kind = Enum.TryParse<CustomerKind>(kind, ignoreCase: true, out var parsed) ? parsed : null,
            Status = CustomerStatus.Active,
            HasAccount = hasAccount,
        }, pageSkip, pageTake, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiPage<ApiCustomer>(page.Select(CustomerAccountApiEndpoints.ToApi).ToList(), count, pageSkip, pageTake));
    }

    private static async Task<Results<Ok<ApiCustomer>, ProblemHttpResult>> GetAsync(
        string customerId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICustomerStore customers)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CustomerPermissions.ViewCustomers))
        {
            return ApiProblems.Forbidden();
        }

        return await customers.FindByIdAsync(customerId, httpContext.RequestAborted) is { Status: CustomerStatus.Active } customer
            ? TypedResults.Ok(CustomerAccountApiEndpoints.ToApi(customer))
            : ApiProblems.NotFound();
    }
}
