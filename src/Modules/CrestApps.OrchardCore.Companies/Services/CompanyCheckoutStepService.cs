using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// The Company step of a company buyer's checkout: the purchase order number the company's invoice carries, required
/// when the company says so. The step has to be saved before the checkout can complete.
/// </summary>
public sealed class CompanyCheckoutStepService
{
    public const string StepKey = "company";

    public const int MaxPurchaseOrderLength = 64;

    private readonly CompanyService _companies;
    private readonly IOrderStore _orders;
    private readonly CompanyApprovalService _approvals;

    internal readonly IStringLocalizer S;

    public CompanyCheckoutStepService(CompanyService companies, IOrderStore orders, CompanyApprovalService approvals, IStringLocalizer<CompanyCheckoutStepService> stringLocalizer)
    {
        _companies = companies;
        _orders = orders;
        _approvals = approvals;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns why the checkout's order needs a company admin's approval it doesn't have yet, or
    /// <see langword="null"/>.
    /// </summary>
    public async Task<string> GetApprovalReasonAsync(CheckoutSession session, Order order, CheckoutInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (order is null || invoice is null || await GetCompanyAsync(session, cancellationToken) is not { } company ||
            order.Approval?.Covers(invoice.GrandTotal, invoice.Currency) == true)
        {
            return null;
        }

        return await _approvals.GetReasonAsync(company, company.FindBuyer(session.OwnerId), invoice.GrandTotal, invoice.Currency, order.ItemId, cancellationToken);
    }

    /// <summary>
    /// Returns the approval the checkout's order is waiting for or was given, and why it needs one now, if it does.
    /// </summary>
    public async Task<(OrderApproval Approval, string Reason)> GetApprovalAsync(ICheckoutFlowSession flowSession, CancellationToken cancellationToken = default)
    {
        if (flowSession is not CheckoutSession session ||
            !session.TryGet<CheckoutInvoice>(out var invoice) ||
            await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is not { } order)
        {
            return (null, null);
        }

        return (order.Approval, await GetApprovalReasonAsync(session, order, invoice, cancellationToken));
    }

    /// <summary>
    /// Returns the company a checkout is for, when its buyer buys for one and it's an order's checkout, or
    /// <see langword="null"/>.
    /// </summary>
    public async Task<Company> GetCompanyAsync(ICheckoutFlowSession flowSession, CancellationToken cancellationToken = default)
    {
        // A sale at a register is the store's, even when the cashier buys for a company.
        if (flowSession is not CheckoutSession { OwnerId.Length: > 0 } session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            session.TryGet<CheckoutCounterSale>(out _) ||
            await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is { Channel: OrderChannels.PointOfSale })
        {
            return null;
        }

        return await _companies.GetForUserAsync(session.OwnerId, cancellationToken);
    }

    /// <summary>
    /// Returns the purchase order number the step was saved with, or <see langword="null"/>.
    /// </summary>
    public static string GetPurchaseOrderNumber(ICheckoutFlowSession session)
        => (session as CheckoutSession)?.SavedSteps?[StepKey]?["PurchaseOrderNumber"]?.GetValue<string>();

    /// <summary>
    /// Saves the step with a purchase order number, and puts it on the checkout's order. Returns the problems, keyed by
    /// field, when it can't be saved.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> SaveAsync(ICheckoutFlowSession flowSession, string purchaseOrderNumber, CancellationToken cancellationToken = default)
        => await SaveAsync(flowSession, purchaseOrderNumber, requestApproval: false, cancellationToken);

    /// <summary>
    /// Saves the step with a purchase order number, and puts it on the checkout's order. An order that needs a company
    /// admin's approval isn't saved: with <paramref name="requestApproval"/>, it's sent for approval. Returns the
    /// problems, keyed by field ("approval" for the approval), when it can't be saved.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> SaveAsync(ICheckoutFlowSession flowSession, string purchaseOrderNumber, bool requestApproval, CancellationToken cancellationToken = default)
    {
        if (flowSession is not CheckoutSession session)
        {
            throw new ArgumentException("A checkout session is required.", nameof(flowSession));
        }

        var problems = new Dictionary<string, string>(StringComparer.Ordinal);

        if (await GetCompanyAsync(session, cancellationToken) is not { } company)
        {
            problems["purchaseOrderNumber"] = S["This checkout isn't for a company."];

            return problems;
        }

        var number = string.IsNullOrWhiteSpace(purchaseOrderNumber) ? null : purchaseOrderNumber.Trim();

        if (number is null && company.Terms?.RequirePurchaseOrder == true)
        {
            problems["purchaseOrderNumber"] = S["{0} needs a purchase order number for every order.", company.Name];
        }
        else if (number?.Length > MaxPurchaseOrderLength)
        {
            problems["purchaseOrderNumber"] = S["A purchase order number is at most {0} characters.", MaxPurchaseOrderLength];
        }

        if (problems.Count > 0)
        {
            return problems;
        }

        var order = await _orders.FindByIdAsync(session.ReferenceId, cancellationToken);

        if (order is not null)
        {
            order.PurchaseOrderNumber = number;
            order.CompanyId ??= company.ItemId;
            await _orders.UpdateAsync(order, cancellationToken);
        }

        if (order is not null && session.TryGet<CheckoutInvoice>(out var invoice) &&
            await GetApprovalReasonAsync(session, order, invoice, cancellationToken) is { } reason)
        {
            if (order.Approval?.Status == OrderApprovalStatus.Pending)
            {
                problems["approval"] = S["This order is waiting for approval. We'll email you when it's decided; pay it from your orders then."];
            }
            else if (requestApproval)
            {
                var buyer = company.FindBuyer(session.OwnerId);

                await _approvals.RequestAsync(company, order, reason, invoice.GrandTotal, invoice.Currency, buyer?.UserName ?? buyer?.Email, cancellationToken);
                problems["approval"] = S["Sent for approval. We'll email you when it's decided; pay it from your orders then."];
            }
            else
            {
                problems["approval"] = S["This order needs a company admin's approval: {0}.", reason];
            }

            return problems;
        }

        session.SavedSteps[StepKey] = new JsonObject { ["PurchaseOrderNumber"] = number };

        return problems;
    }
}
