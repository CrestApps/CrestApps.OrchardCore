using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Companies.Api;

/// <summary>
/// A company buyer's side of the API: the company they buy for, its account, and the Company step and account payment
/// of their checkouts.
/// </summary>
internal static class CompanyApiEndpoints
{
    public const string Tag = "Companies";

    public static IEndpointRouteBuilder MapCompanyApi(this IEndpointRouteBuilder routes)
    {
        var account = routes.MapCommerceApi(Tag);

        account.MapGet("account/company", GetCompanyAsync)
            .WithName("GetAccountCompany")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets the company the signed-in buyer buys for: their role, its terms, locations and what it owes on account.");

        var checkout = routes.MapCommerceApi(CheckoutApi.Tag);

        checkout.MapPut("checkout/{id}/company", SaveCompanyStepAsync)
            .WithName("SaveCheckoutCompany")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Saves the Company step of a company buyer's checkout: the purchase order number, required when the company says so.");

        checkout.MapPost("checkout/{id}/account", ApplyAccountAsync)
            .WithName("ApplyCheckoutAccount")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Puts what the company's deposit doesn't cover on the company's account, invoiced and due in its net days.");

        checkout.MapDelete("checkout/{id}/account", RemoveAccountAsync)
            .WithName("RemoveCheckoutAccount")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes the account payment off the checkout, so it's paid in full at checkout.");

        return routes;
    }

    private static async Task<Results<Ok<ApiCompany>, ProblemHttpResult>> GetCompanyAsync(HttpContext httpContext, CompanyService companies, CompanyTermsService terms)
    {
        var (company, buyer) = await companies.GetCurrentAsync(httpContext.RequestAborted);

        if (company is null || buyer is null)
        {
            return ApiProblems.NotFound("You don't buy for a company.");
        }

        var (owed, currency) = await terms.GetOwedAsync(company, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiCompany(
            company.ItemId,
            company.Name,
            company.TaxId,
            buyer.Role == CompanyBuyerRole.Admin ? "admin" : "buyer",
            ApiMoney.From(buyer.OrderLimit, currency),
            company.Terms?.OffersTerms == true ? new ApiCompanyTerms(company.Terms.NetDays ?? 0, company.Terms.DepositPercent, ApiMoney.From(company.Terms.CreditLimit, currency), ApiMoney.From(owed, currency)) : null,
            company.Terms?.RequirePurchaseOrder == true,
            [.. company.Locations.Select(location => new ApiCompanyLocation(location.LocationId, location.Name, ApiAddress.From(location.Address)))]));
    }

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> SaveCompanyStepAsync(
        string id,
        ApiCompanyStepRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CompanyCheckoutStepService step)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        if (await step.GetCompanyAsync(session, httpContext.RequestAborted) is null)
        {
            return ApiProblems.NotFound("This checkout isn't for a company.");
        }

        var problems = await step.SaveAsync(session, request?.PurchaseOrderNumber, httpContext.RequestAborted);

        if (problems.Count > 0)
        {
            return ApiProblems.Validation(problems.ToDictionary(problem => problem.Key, problem => new[] { problem.Value }));
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> ApplyAccountAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CompanyTermsService terms,
        CheckoutTenderChoices choices)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (!CheckoutTenderChoices.AcceptsTenders(flow) || !session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "This checkout can't be paid on account.");
        }

        if (await terms.GetForCheckoutAsync(flow, invoice, httpContext.RequestAborted) is not { Company: { } company, Amount: > 0m })
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "There's no company account to pay this checkout on, or no credit left on it.");
        }

        await choices.AddAsync(flow, CompaniesConstants.TermsTenderKey, CompanyTermsService.ToReference(company.ItemId, session.SessionId));
        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemoveAccountAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutTenderChoices choices)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        foreach (var tender in session.TryGet<CheckoutInvoice>(out var invoice) ? (invoice.Tenders ?? []).Where(entry => entry.ProviderKey == CompaniesConstants.TermsTenderKey).ToList() : [])
        {
            await choices.RemoveAsync(flow, CompaniesConstants.TermsTenderKey, tender.Reference);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }
}

/// <summary>
/// The company a buyer buys for.
/// </summary>
/// <param name="Role"><c>buyer</c> or <c>admin</c>.</param>
/// <param name="OrderLimit">The most an order of theirs may come to before an admin has to approve it.</param>
/// <param name="Terms">How the company pays on account, or none when it pays at checkout.</param>
public sealed record ApiCompany(string Id, string Name, string TaxId, string Role, ApiMoney OrderLimit, ApiCompanyTerms Terms, bool RequirePurchaseOrder, IReadOnlyList<ApiCompanyLocation> Locations);

/// <summary>
/// A company's terms: its invoices are due in <paramref name="NetDays"/>, after a deposit at checkout.
/// </summary>
public sealed record ApiCompanyTerms(int NetDays, decimal DepositPercent, ApiMoney CreditLimit, ApiMoney Owed);

/// <summary>
/// A place a company has orders shipped to.
/// </summary>
public sealed record ApiCompanyLocation(string Id, string Name, ApiAddress Address);

/// <summary>
/// The Company step of a checkout.
/// </summary>
public sealed class ApiCompanyStepRequest
{
    public string PurchaseOrderNumber { get; set; }
}
