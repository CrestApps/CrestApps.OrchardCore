using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Accounts.B2B.Drivers;

/// <summary>
/// The "Pay on account" box on the Payment step, for a buyer whose account has terms: one click puts what the deposit
/// doesn't cover on the account's terms.
/// </summary>
internal sealed class AccountTermsCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    internal const string ApplyAction = CheckoutConstants.FormActions.ApplyTender + ":" + B2BAccountsConstants.TermsTenderKey;

    private readonly AccountTermsService _terms;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutTenderChoices _choices;

    public AccountTermsCheckoutFlowDisplayDriver(
        AccountTermsService terms,
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

        var (account, amount) = await _terms.GetForCheckoutAsync(flow, invoice);
        var tender = (invoice.Tenders ?? []).FirstOrDefault(entry => entry.ProviderKey == B2BAccountsConstants.TermsTenderKey);

        if (account is null || (tender is null && (amount <= 0m || invoice.InitialPaymentAmount is not > 0m)))
        {
            return null;
        }

        return Initialize<AccountTermsCheckoutViewModel>("CheckoutAccountTerms", model =>
        {
            model.Reference = AccountTermsService.ToReference(account.AccountId, flow.Session.SessionId);
            model.AccountName = account.Name;
            model.NetDays = account.Terms.NetDays ?? 0;
            model.DepositPercent = account.Terms.DepositPercent;
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
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + B2BAccountsConstants.TermsTenderKey + ":";

        if (string.Equals(action, ApplyAction, StringComparison.Ordinal) &&
            flow.Session.TryGet<CheckoutInvoice>(out var invoice) &&
            await _terms.GetForCheckoutAsync(flow, invoice) is { Account: { } account, Amount: > 0m })
        {
            await _choices.AddAsync(flow, B2BAccountsConstants.TermsTenderKey, AccountTermsService.ToReference(account.AccountId, flow.Session.SessionId));
        }
        else if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            await _choices.RemoveAsync(flow, B2BAccountsConstants.TermsTenderKey, action[removePrefix.Length..]);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool AcceptsTerms(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false, OwnerId.Length: > 0 };
}

public class AccountTermsCheckoutViewModel
{
    public string Reference { get; set; }

    public string AccountName { get; set; }

    public int NetDays { get; set; }

    public decimal DepositPercent { get; set; }

    public string Currency { get; set; }

    public decimal OnAccount { get; set; }

    public bool IsApplied { get; set; }
}
