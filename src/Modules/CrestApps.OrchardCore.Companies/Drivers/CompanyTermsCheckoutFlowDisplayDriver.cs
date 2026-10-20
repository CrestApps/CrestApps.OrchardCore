using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Companies.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Companies.Drivers;

/// <summary>
/// The "Pay on account" box on the Payment step, for a buyer whose company has terms: one click puts what the deposit
/// doesn't cover on the company's account.
/// </summary>
internal sealed class CompanyTermsCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    internal const string ApplyAction = CheckoutConstants.FormActions.ApplyTender + ":" + CompaniesConstants.TermsTenderKey;

    private readonly CompanyTermsService _terms;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutTenderChoices _choices;

    public CompanyTermsCheckoutFlowDisplayDriver(
        CompanyTermsService terms,
        IHttpContextAccessor httpContextAccessor,
        CheckoutTenderChoices choices)
    {
        _terms = terms;
        _httpContextAccessor = httpContextAccessor;
        _choices = choices;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!AcceptsTerms(flow) || !flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        var (company, amount) = await _terms.GetForCheckoutAsync(flow, invoice);
        var tender = (invoice.Tenders ?? []).FirstOrDefault(entry => entry.ProviderKey == CompaniesConstants.TermsTenderKey);

        if (company is null || (tender is null && (amount <= 0m || invoice.InitialPaymentAmount is not > 0m)))
        {
            return null;
        }

        return Initialize<CompanyTermsCheckoutViewModel>("CheckoutCompanyTerms", model =>
        {
            model.Reference = CompanyTermsService.ToReference(company.ItemId, flow.Session.SessionId);
            model.CompanyName = company.Name;
            model.NetDays = company.Terms.NetDays ?? 0;
            model.DepositPercent = company.Terms.DepositPercent;
            model.Currency = invoice.Currency;
            model.OnAccount = tender?.Amount ?? amount;
            model.IsApplied = tender is not null;
        }).Location("Aside:6");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!AcceptsTerms(flow))
        {
            return null;
        }

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + CompaniesConstants.TermsTenderKey + ":";

        if (string.Equals(action, ApplyAction, StringComparison.Ordinal) &&
            flow.Session.TryGet<CheckoutInvoice>(out var invoice) &&
            await _terms.GetForCheckoutAsync(flow, invoice) is { Company: { } company, Amount: > 0m })
        {
            await _choices.AddAsync(flow, CompaniesConstants.TermsTenderKey, CompanyTermsService.ToReference(company.ItemId, flow.Session.SessionId));
        }
        else if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            await _choices.RemoveAsync(flow, CompaniesConstants.TermsTenderKey, action[removePrefix.Length..]);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool AcceptsTerms(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false, OwnerId.Length: > 0 };
}

public class CompanyTermsCheckoutViewModel
{
    public string Reference { get; set; }

    public string CompanyName { get; set; }

    public int NetDays { get; set; }

    public decimal DepositPercent { get; set; }

    public string Currency { get; set; }

    public decimal OnAccount { get; set; }

    public bool IsApplied { get; set; }
}
