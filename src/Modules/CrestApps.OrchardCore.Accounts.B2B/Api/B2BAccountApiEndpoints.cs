using CrestApps.OrchardCore.Accounts.B2B.Controllers;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Accounts.B2B.Api;

/// <summary>
/// An account buyer's side of the API: the account they buy for, what it owes, and the Business account step and account payment
/// of their checkouts.
/// </summary>
internal static class B2BAccountApiEndpoints
{
    public const string Tag = "B2B Accounts";

    public static IEndpointRouteBuilder MapB2BAccountApi(this IEndpointRouteBuilder routes)
    {
        var account = routes.MapCommerceApi(Tag);

        account.MapGet("account/business", GetB2BAccountAsync)
            .WithName("GetBusinessAccount")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets the account the signed-in buyer buys for: their role, its terms, locations and what it owes on account.");

        var checkout = routes.MapCommerceApi(CheckoutApi.Tag);

        checkout.MapPut("checkout/{id}/business", SaveB2BAccountStepAsync)
            .WithName("SaveCheckoutB2BAccount")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Saves the Business account step of an account buyer's checkout: the purchase order number, required when the account says so.");

        account.MapGet("account/business/approvals", ListApprovalsAsync)
            .WithName("ListMyB2BApprovals")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the account's orders waiting for an admin's approval, for an account admin.");

        account.MapPost("account/business/approvals/{orderId}", DecideAsync)
            .WithName("DecideMyB2BApproval")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Approves or rejects an order waiting for an account admin's approval. A rejected order is canceled.");

        checkout.MapPost("checkout/{id}/account", ApplyAccountAsync)
            .WithName("ApplyCheckoutAccount")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Puts what the account's deposit doesn't cover on the account's terms, invoiced and due in its net days.");

        checkout.MapDelete("checkout/{id}/account", RemoveAccountAsync)
            .WithName("RemoveCheckoutAccount")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes the account payment off the checkout, so it's paid in full at checkout.");

        return routes;
    }

    private static async Task<Results<Ok<ApiB2BAccount>, ProblemHttpResult>> GetB2BAccountAsync(HttpContext httpContext, B2BAccountService accounts, AccountTermsService terms)
    {
        var (account, buyer) = await accounts.GetCurrentAsync(httpContext.RequestAborted);

        if (account is null || buyer is null)
        {
            return ApiProblems.NotFound("You don't buy for an account.");
        }

        var (owed, currency) = await terms.GetOwedAsync(account, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiB2BAccount(
            account.AccountId,
            account.Name,
            account.TaxId,
            buyer.Role == AccountMemberRole.Admin ? "admin" : "buyer",
            ApiMoney.From(buyer.OrderLimit, currency),
            account.Terms?.OffersTerms == true ? new ApiAccountTerms(account.Terms.NetDays ?? 0, account.Terms.DepositPercent, ApiMoney.From(account.Terms.CreditLimit, currency), ApiMoney.From(owed, currency)) : null,
            account.Terms?.RequirePurchaseOrder == true,
            [.. account.Locations.Select(location => new ApiB2BAccountLocation(location.LocationId, location.Name, ApiAddress.From(location.Address)))]));
    }

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> SaveB2BAccountStepAsync(
        string id,
        ApiB2BAccountStepRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        B2BCheckoutStepService step)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        if (await step.GetB2BAccountAsync(session, httpContext.RequestAborted) is null)
        {
            return ApiProblems.NotFound("This checkout isn't for an account.");
        }

        var problems = await step.SaveAsync(session, request?.PurchaseOrderNumber, request?.RequestApproval == true, httpContext.RequestAborted);

        if (problems.Count > 0)
        {
            return ApiProblems.Validation(problems.ToDictionary(problem => problem.Key, problem => new[] { problem.Value }));
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiB2BAccountApproval>>, ProblemHttpResult>> ListApprovalsAsync(HttpContext httpContext, B2BAccountService accounts, IOrderStore orders)
    {
        var (account, buyer) = await accounts.GetCurrentAsync(httpContext.RequestAborted);

        if (account is null || buyer?.Role != AccountMemberRole.Admin)
        {
            return ApiProblems.NotFound("You aren't an admin of an account.");
        }

        var waiting = await BusinessAccountController.ListApprovalsAsync(orders, account.AccountId, httpContext.RequestAborted);

        return TypedResults.Ok<IReadOnlyList<ApiB2BAccountApproval>>(waiting
            .Select(order => new ApiB2BAccountApproval(order.ItemId, order.OrderNumber, ApiMoney.From(order.Approval.Amount, order.Approval.Currency), order.Approval.Reason, order.Approval.RequestedBy, order.Approval.RequestedUtc))
            .ToList());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DecideAsync(
        string orderId,
        ApiB2BAccountDecision decision,
        HttpContext httpContext,
        B2BAccountService accounts,
        IOrderStore orders,
        B2BApprovalService approvals)
    {
        var (account, buyer) = await accounts.GetCurrentAsync(httpContext.RequestAborted);

        if (account is null || buyer?.Role != AccountMemberRole.Admin ||
            await orders.FindByIdAsync(orderId, httpContext.RequestAborted) is not { } order || order.AccountId != account.AccountId)
        {
            return ApiProblems.NotFound();
        }

        if (!await approvals.DecideAsync(order, decision?.Approve == true, buyer.UserName ?? buyer.Email, decision?.Note, httpContext.RequestAborted))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "The order isn't waiting for approval.");
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> ApplyAccountAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        AccountTermsService terms,
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

        if (await terms.GetForCheckoutAsync(flow, invoice, httpContext.RequestAborted) is not { Account: { } account, Amount: > 0m })
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, "There's no business account to pay this checkout on, or no credit left on it.");
        }

        await choices.AddAsync(flow, B2BAccountsConstants.TermsTenderKey, AccountTermsService.ToReference(account.AccountId, session.SessionId));
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

        foreach (var tender in session.TryGet<CheckoutInvoice>(out var invoice) ? (invoice.Tenders ?? []).Where(entry => entry.ProviderKey == B2BAccountsConstants.TermsTenderKey).ToList() : [])
        {
            await choices.RemoveAsync(flow, B2BAccountsConstants.TermsTenderKey, tender.Reference);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }
}

/// <summary>
/// The account a buyer buys for.
/// </summary>
/// <param name="Role"><c>buyer</c> or <c>admin</c>.</param>
/// <param name="OrderLimit">The most an order of theirs may come to before an admin has to approve it.</param>
/// <param name="Terms">How the account pays on account, or none when it pays at checkout.</param>
public sealed record ApiB2BAccount(string Id, string Name, string TaxId, string Role, ApiMoney OrderLimit, ApiAccountTerms Terms, bool RequirePurchaseOrder, IReadOnlyList<ApiB2BAccountLocation> Locations);

/// <summary>
/// An account's terms: its invoices are due in <paramref name="NetDays"/>, after a deposit at checkout.
/// </summary>
public sealed record ApiAccountTerms(int NetDays, decimal DepositPercent, ApiMoney CreditLimit, ApiMoney Owed);

/// <summary>
/// A place an account has orders shipped to.
/// </summary>
public sealed record ApiB2BAccountLocation(string Id, string Name, ApiAddress Address);

/// <summary>
/// The Business account step of a checkout.
/// </summary>
public sealed class ApiB2BAccountStepRequest
{
    public string PurchaseOrderNumber { get; set; }

    /// <summary>
    /// Gets or sets whether to ask the account's admins to approve an order that needs it. The answer is then a
    /// problem on <c>approval</c> saying it was sent; the order is paid from the buyer's orders once approved.
    /// </summary>
    public bool RequestApproval { get; set; }
}

/// <summary>
/// An order waiting for an account admin's approval.
/// </summary>
public sealed record ApiB2BAccountApproval(string OrderId, string OrderNumber, ApiMoney Amount, string Reason, string RequestedBy, DateTime RequestedUtc);

/// <summary>
/// An account admin's decision on an order.
/// </summary>
public sealed record ApiB2BAccountDecision(bool Approve, string Note);
