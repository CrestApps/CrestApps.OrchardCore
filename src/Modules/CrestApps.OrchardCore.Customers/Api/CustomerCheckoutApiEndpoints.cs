using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Customers.Api;

/// <summary>
/// The checkout steps Customers adds, over the API: the buyer's contact details, and their store credit and points.
/// </summary>
internal static class CustomerCheckoutApiEndpoints
{
    public static IEndpointRouteBuilder MapCustomerCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapPut("checkout/{id}/contact", SaveContactAsync)
            .AllowGuests()
            .WithName("SaveCheckoutContact")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Saves the Contact step: who the buyer is, and their billing address when the store asks for one.");

        return routes;
    }

    public static IEndpointRouteBuilder MapStoreCreditCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapPost("checkout/{id}/store-credit", ApplyStoreCreditAsync)
            .WithName("ApplyCheckoutStoreCredit")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Puts the signed-in buyer's store credit toward the checkout.");

        group.MapDelete("checkout/{id}/store-credit", RemoveStoreCreditAsync)
            .WithName("RemoveCheckoutStoreCredit")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes the buyer's store credit off the checkout.");

        return routes;
    }

    public static IEndpointRouteBuilder MapLoyaltyCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapPost("checkout/{id}/points", ApplyPointsAsync)
            .WithName("ApplyCheckoutPoints")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Spends the signed-in buyer's loyalty points on the checkout.");

        group.MapDelete("checkout/{id}/points", RemovePointsAsync)
            .WithName("RemoveCheckoutPoints")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes the buyer's points off the checkout.");

        return routes;
    }

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> SaveContactAsync(
        string id,
        ApiContactRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CustomerContactStepService contactStep)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var modelState = new ModelStateDictionary();
        var saved = await contactStep.SaveAsync(new CheckoutFlow(session), new CustomerContactInput
        {
            Name = request?.Name,
            Email = request?.Email,
            Phone = request?.Phone,
            BillingAddressId = request?.BillingAddressId,
            BillingAddress = ApiAddressEditor.ToEditor(request?.BillingAddress),
            SaveBillingAddress = request?.SaveBillingAddress == true,
        }, modelState, prefix: null);

        if (!saved)
        {
            return ApiProblems.Validation(modelState);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> ApplyStoreCreditAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        StoreCreditTenderProvider provider,
        CheckoutTenderChoices choices)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (!CheckoutTenderChoices.AcceptsTenders(flow) || string.IsNullOrEmpty(session.OwnerId) || !session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Store credit can't pay for this checkout.");
        }

        if (await provider.FindForCheckoutAsync(flow, invoice.Currency, httpContext.RequestAborted) is not { Balance: > 0m } account)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "There's no store credit to use.");
        }

        await choices.AddAsync(flow, StoreCreditService.TenderKey, account.ItemId);
        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemoveStoreCreditAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutTenderChoices choices)
        => RemoveTenderAsync(id, StoreCreditService.TenderKey, httpContext, store, choices);

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> ApplyPointsAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        LoyaltyTenderProvider provider,
        CheckoutTenderChoices choices)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (!CheckoutTenderChoices.AcceptsTenders(flow) || string.IsNullOrEmpty(session.OwnerId))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Points can't pay for this checkout.");
        }

        if (await provider.FindForCheckoutAsync(flow, httpContext.RequestAborted) is not { Points: > 0 } account)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "There are no points to spend.");
        }

        // Too few points to redeem yet are chosen but pay nothing; the invoice says so.
        await choices.AddAsync(flow, LoyaltyService.TenderKey, account.ItemId);
        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemovePointsAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutTenderChoices choices)
        => RemoveTenderAsync(id, LoyaltyService.TenderKey, httpContext, store, choices);

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemoveTenderAsync(
        string id,
        string providerKey,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutTenderChoices choices)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);
        var removed = false;

        foreach (var reference in session.TryGet<CheckoutTenderSelection>(out var selection) ? selection.For(providerKey).ToArray() : [])
        {
            removed |= await choices.RemoveAsync(flow, providerKey, reference);
        }

        if (removed)
        {
            await store.SaveAsync(session, httpContext.RequestAborted);
        }

        return TypedResults.Ok(CheckoutApi.Read(session));
    }
}

/// <summary>
/// The Contact step.
/// </summary>
public sealed class ApiContactRequest
{
    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// One of the signed-in buyer's saved addresses to bill to, instead of <see cref="BillingAddress"/>.
    /// </summary>
    public string BillingAddressId { get; set; }

    public ApiAddress BillingAddress { get; set; }

    /// <summary>
    /// Whether a signed-in buyer keeps the billing address in their address book.
    /// </summary>
    public bool? SaveBillingAddress { get; set; }
}

/// <summary>
/// Turns an address an API client sent into the address editor's model, which validates it by its country's format.
/// </summary>
internal static class ApiAddressEditor
{
    public static AddressEditorViewModel ToEditor(ApiAddress address)
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
