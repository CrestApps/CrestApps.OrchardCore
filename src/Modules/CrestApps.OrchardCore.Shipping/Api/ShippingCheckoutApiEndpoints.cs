using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Api;

/// <summary>
/// The Shipping step over the API: where the order can be picked up, the ways to ship it to an address, and the choice.
/// </summary>
internal static class ShippingCheckoutApiEndpoints
{
    public static IEndpointRouteBuilder MapShippingCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapGet("checkout/{id}/pickup-locations", GetPickupLocationsAsync)
            .AllowGuests()
            .WithName("ListCheckoutPickupLocations")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the store's locations the order can be picked up from, and what picking up costs.");

        group.MapPost("checkout/{id}/shipping-rates", QuoteAsync)
            .AllowGuests()
            .WithName("QuoteCheckoutShipping")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the ways to ship the order to an address, with what each costs.");

        group.MapPut("checkout/{id}/shipping", SaveAsync)
            .AllowGuests()
            .WithName("SaveCheckoutShipping")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Saves the Shipping step: a pickup location, or an address and one of its rates. With several rates and none chosen, the answer asks for rateId.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiPickupLocation>>, ProblemHttpResult>> GetPickupLocationsAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ShippingCheckoutStepService shipping)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var currency = session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.Currency : session.Currency;
        var locations = (await shipping.GetPickupLocationsAsync())
            .Select(location => new ApiPickupLocation(location.LocationId, location.Name, ApiAddress.From(location.ToAddress()), location.Instructions, ApiMoney.From(location.Fee, currency)))
            .ToList();

        return TypedResults.Ok<IReadOnlyList<ApiPickupLocation>>(locations);
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiShippingRate>>, ValidationProblem, ProblemHttpResult>> QuoteAsync(
        string id,
        ApiShippingRatesRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ShippingCheckoutStepService shipping)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);
        var address = await GetAddressAsync(shipping, session, request?.AddressId, request?.Address);

        if (string.IsNullOrEmpty(address?.Country))
        {
            return ApiProblems.Validation("address.country", "Give the address's country, at least.");
        }

        if (await shipping.QuoteAsync(flow, address) is not { } quote)
        {
            return ApiProblems.NotFound("The order this checkout pays for is gone.");
        }

        return TypedResults.Ok(ToRates(quote, session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> SaveAsync(
        string id,
        ApiShippingRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ShippingCheckoutStepService shipping)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var modelState = new ModelStateDictionary();
        var result = await shipping.SaveAsync(new CheckoutFlow(session), new ShippingStepInput
        {
            PickupLocationId = request?.PickupLocationId,
            AddressId = request?.AddressId,
            Address = ToEditor(request?.Address),
            RateId = request?.RateId,
            SaveAddress = request?.SaveAddress == true,
        }, modelState, prefix: null);

        if (result.IsChoosing)
        {
            return ApiProblems.Validation("rateId", "Choose one of the ways to ship to this address; shipping-rates lists them.");
        }

        if (!result.Saved)
        {
            return ApiProblems.Validation(modelState);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static IReadOnlyList<ApiShippingRate> ToRates(ShippingQuote quote, CheckoutSession session)
    {
        var fallback = session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.Currency : session.Currency;

        return (quote.Rates ?? [])
            .Select(rate => new ApiShippingRate(rate.RateId, rate.Name, rate.Description, rate.ProviderName, ApiMoney.From(rate.Amount, rate.Currency ?? fallback)))
            .ToList();
    }

    private static async Task<Address> GetAddressAsync(ShippingCheckoutStepService shipping, CheckoutSession session, string addressId, ApiAddress address)
    {
        if (!string.IsNullOrEmpty(addressId) && await shipping.GetCustomerAsync(session) is { } customer && customer.FindAddress(addressId) is { } saved)
        {
            return saved.ToAddress();
        }

        return ToEditor(address)?.ToAddress();
    }

    private static AddressEditorViewModel ToEditor(ApiAddress address)
        => address is null
            ? null
            : new AddressEditorViewModel
            {
                Name = address.Name,
                Company = address.Company,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                DependentLocality = address.DependentLocality,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                SortingCode = address.SortingCode,
                Country = address.Country,
                Phone = address.Phone,
            };
}

/// <summary>
/// An address to quote shipping to: one of the signed-in buyer's saved addresses, or a new one.
/// </summary>
public sealed class ApiShippingRatesRequest
{
    public string AddressId { get; set; }

    public ApiAddress Address { get; set; }
}

/// <summary>
/// The Shipping step: a pickup location; or an address (saved or new) and the rate chosen for it.
/// </summary>
public sealed class ApiShippingRequest
{
    public string PickupLocationId { get; set; }

    public string AddressId { get; set; }

    public ApiAddress Address { get; set; }

    /// <summary>
    /// The rate chosen from the address's quote; not needed when there's only one.
    /// </summary>
    public string RateId { get; set; }

    /// <summary>
    /// Whether a signed-in buyer keeps the address in their address book.
    /// </summary>
    public bool? SaveAddress { get; set; }
}

/// <summary>
/// A way to ship the order, and what it costs.
/// </summary>
public sealed record ApiShippingRate(string RateId, string Name, string Description, string Provider, ApiMoney Amount);

/// <summary>
/// A store location the order can be picked up from.
/// </summary>
public sealed record ApiPickupLocation(string Id, string Name, ApiAddress Address, string Instructions, ApiMoney Fee);
