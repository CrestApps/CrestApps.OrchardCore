using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Customers.Api;

/// <summary>
/// The signed-in buyer's own account over the API: their details and their address book, the same as the storefront's
/// account pages, with the same permission (managing one's own customer profile).
/// </summary>
internal static class CustomerAccountApiEndpoints
{
    public const string Tag = "Account";

    public static IEndpointRouteBuilder MapCustomerAccountApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("me", GetAsync)
            .WithName("GetMyAccount")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Gets the signed-in buyer's customer record: their details and addresses.");

        group.MapPut("me", UpdateAsync)
            .WithName("UpdateMyAccount")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Changes the signed-in buyer's details. Their email belongs to their user account, so it isn't changed here.");

        group.MapPost("me/addresses", AddAddressAsync)
            .WithName("AddMyAddress")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Adds an address to the signed-in buyer's address book.");

        group.MapPut("me/addresses/{addressId}", UpdateAddressAsync)
            .WithName("UpdateMyAddress")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Changes an address in the signed-in buyer's address book.");

        group.MapDelete("me/addresses/{addressId}", DeleteAddressAsync)
            .WithName("DeleteMyAddress")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Deletes an address from the signed-in buyer's address book.");

        return routes;
    }

    private static async Task<Results<Ok<ApiCustomer>, ProblemHttpResult>> GetAsync(HttpContext httpContext, CustomerAccountPages pages)
        => await pages.GetCustomerAsync(httpContext.User, httpContext.RequestAborted) is { } customer
            ? TypedResults.Ok(ToApi(customer))
            : ApiProblems.Forbidden("This account has no customer record it may manage.");

    private static async Task<Results<Ok<ApiCustomer>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        ApiCustomerUpdate request,
        HttpContext httpContext,
        CustomerAccountPages pages)
    {
        if (await pages.GetCustomerAsync(httpContext.User, httpContext.RequestAborted) is not { } customer)
        {
            return ApiProblems.Forbidden("This account has no customer record it may manage.");
        }

        var model = new MyAccountViewModel
        {
            Kind = string.Equals(request?.Kind, "business", StringComparison.OrdinalIgnoreCase) ? CustomerKind.Business : CustomerKind.Person,
            DisplayName = request?.DisplayName,
            FirstName = request?.FirstName,
            LastName = request?.LastName,
            CompanyName = request?.CompanyName,
            TaxId = request?.TaxId,
            Phone = request?.Phone,
            AcceptsMarketing = request?.AcceptsMarketing == true,
        };
        var modelState = new ModelStateDictionary();

        return await pages.SaveProfileAsync(customer, model, modelState, httpContext.RequestAborted) switch
        {
            CustomerAccountSaveResult.Invalid => ApiProblems.Validation(modelState),
            CustomerAccountSaveResult.Conflict => ConcurrencyProblem(),
            _ => TypedResults.Ok(ToApi(customer)),
        };
    }

    private static async Task<Results<Created<ApiCustomerAddress>, ValidationProblem, ProblemHttpResult>> AddAddressAsync(
        ApiCustomerAddressRequest request,
        HttpContext httpContext,
        CustomerAccountPages pages)
    {
        if (await pages.GetCustomerAsync(httpContext.User, httpContext.RequestAborted) is not { } customer)
        {
            return ApiProblems.Forbidden("This account has no customer record it may manage.");
        }

        var modelState = new ModelStateDictionary();
        var (result, entry) = await pages.SaveAddressAsync(customer, addressId: null, ToModel(request), modelState, httpContext.RequestAborted);

        return result switch
        {
            CustomerAccountSaveResult.Invalid => ApiProblems.Validation(modelState),
            CustomerAccountSaveResult.Conflict => ConcurrencyProblem(),
            _ => TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/me/addresses/{entry.AddressId}", ToApi(customer, entry)),
        };
    }

    private static async Task<Results<Ok<ApiCustomerAddress>, ValidationProblem, ProblemHttpResult>> UpdateAddressAsync(
        string addressId,
        ApiCustomerAddressRequest request,
        HttpContext httpContext,
        CustomerAccountPages pages)
    {
        if (await pages.GetCustomerAsync(httpContext.User, httpContext.RequestAborted) is not { } customer)
        {
            return ApiProblems.Forbidden("This account has no customer record it may manage.");
        }

        var modelState = new ModelStateDictionary();
        var (result, entry) = await pages.SaveAddressAsync(customer, addressId, ToModel(request), modelState, httpContext.RequestAborted);

        return result switch
        {
            CustomerAccountSaveResult.NotFound => ApiProblems.NotFound(),
            CustomerAccountSaveResult.Invalid => ApiProblems.Validation(modelState),
            CustomerAccountSaveResult.Conflict => ConcurrencyProblem(),
            _ => TypedResults.Ok(ToApi(customer, entry)),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAddressAsync(
        string addressId,
        HttpContext httpContext,
        CustomerAccountPages pages)
    {
        if (await pages.GetCustomerAsync(httpContext.User, httpContext.RequestAborted) is not { } customer)
        {
            return ApiProblems.Forbidden("This account has no customer record it may manage.");
        }

        return await pages.DeleteAddressAsync(customer, addressId, httpContext.RequestAborted) switch
        {
            CustomerAccountSaveResult.NotFound => ApiProblems.NotFound(),
            CustomerAccountSaveResult.Conflict => ConcurrencyProblem(),
            _ => TypedResults.NoContent(),
        };
    }

    private static ProblemHttpResult ConcurrencyProblem()
        => ApiProblems.Conflict(ApiProblems.Codes.Concurrency, "The account was changed somewhere else at the same time. Read it again and retry.");

    private static CustomerAddressViewModel ToModel(ApiCustomerAddressRequest request)
        => new()
        {
            Label = request?.Label,
            Address = ApiAddressEditor.ToEditor(request?.Address),
            IsDefaultBilling = request?.IsDefaultBilling == true,
            IsDefaultShipping = request?.IsDefaultShipping == true,
        };

    internal static ApiCustomer ToApi(Customer customer)
        => new(
            customer.ItemId,
            customer.Kind == CustomerKind.Business ? "business" : "person",
            customer.GetName(),
            customer.DisplayName,
            customer.FirstName,
            customer.LastName,
            customer.CompanyName,
            customer.TaxId,
            customer.Email,
            customer.Phone,
            customer.AcceptsMarketing,
            (customer.Addresses ?? []).Select(entry => ToApi(customer, entry)).ToList(),
            customer.CreatedUtc);

    internal static ApiCustomerAddress ToApi(Customer customer, CustomerAddress entry)
        => new(
            entry.AddressId,
            entry.Label,
            ApiAddress.From(entry.Address),
            string.Equals(customer.DefaultBillingAddressId, entry.AddressId, StringComparison.Ordinal),
            string.Equals(customer.DefaultShippingAddressId, entry.AddressId, StringComparison.Ordinal));
}

/// <summary>
/// A customer: who buys from the store.
/// </summary>
/// <param name="Kind"><c>person</c> or <c>business</c>.</param>
/// <param name="Name">The name to address them by.</param>
public sealed record ApiCustomer(
    string Id,
    string Kind,
    string Name,
    string DisplayName,
    string FirstName,
    string LastName,
    string CompanyName,
    string TaxId,
    string Email,
    string Phone,
    bool AcceptsMarketing,
    IReadOnlyList<ApiCustomerAddress> Addresses,
    DateTime CreatedUtc);

/// <summary>
/// An address in a customer's address book.
/// </summary>
public sealed record ApiCustomerAddress(string Id, string Label, ApiAddress Address, bool IsDefaultBilling, bool IsDefaultShipping);

/// <summary>
/// The signed-in buyer's details.
/// </summary>
public sealed class ApiCustomerUpdate
{
    /// <summary>
    /// <c>person</c> or <c>business</c>; a business needs a company name.
    /// </summary>
    public string Kind { get; set; }

    public string DisplayName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string CompanyName { get; set; }

    public string TaxId { get; set; }

    public string Phone { get; set; }

    public bool? AcceptsMarketing { get; set; }
}

/// <summary>
/// An address for the address book.
/// </summary>
public sealed class ApiCustomerAddressRequest
{
    /// <summary>
    /// What the buyer calls it, such as Home.
    /// </summary>
    public string Label { get; set; }

    public ApiAddress Address { get; set; }

    public bool? IsDefaultBilling { get; set; }

    public bool? IsDefaultShipping { get; set; }
}
