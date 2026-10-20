using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
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

    internal readonly IStringLocalizer S;

    public CompanyCheckoutStepService(CompanyService companies, IOrderStore orders, IStringLocalizer<CompanyCheckoutStepService> stringLocalizer)
    {
        _companies = companies;
        _orders = orders;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the company a checkout is for, when its buyer buys for one and it's an order's checkout, or
    /// <see langword="null"/>.
    /// </summary>
    public async Task<Company> GetCompanyAsync(ICheckoutFlowSession flowSession, CancellationToken cancellationToken = default)
        => flowSession is CheckoutSession { OwnerId.Length: > 0 } session &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !session.TryGet<CheckoutCounterSale>(out _)
                ? await _companies.GetForUserAsync(session.OwnerId, cancellationToken)
                : null;

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

        session.SavedSteps[StepKey] = new JsonObject { ["PurchaseOrderNumber"] = number };

        if (await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is { } order)
        {
            order.PurchaseOrderNumber = number;
            order.CompanyId ??= company.ItemId;
            await _orders.UpdateAsync(order, cancellationToken);
        }

        return problems;
    }
}
