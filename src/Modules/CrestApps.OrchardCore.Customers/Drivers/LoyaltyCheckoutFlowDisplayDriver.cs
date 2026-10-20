using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// The loyalty points box on the Payment step, for a signed-in buyer who has points: one click spends them.
/// </summary>
internal sealed class LoyaltyCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    internal const string ApplyAction = CheckoutConstants.FormActions.ApplyTender + ":" + LoyaltyService.TenderKey;

    private readonly LoyaltyTenderProvider _provider;
    private readonly LoyaltyService _loyalty;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutTenderChoices _choices;

    public LoyaltyCheckoutFlowDisplayDriver(
        LoyaltyTenderProvider provider,
        LoyaltyService loyalty,
        IHttpContextAccessor httpContextAccessor,
        CheckoutTenderChoices choices)
    {
        _provider = provider;
        _loyalty = loyalty;
        _httpContextAccessor = httpContextAccessor;
        _choices = choices;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!AcceptsPoints(flow) || !flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        var account = await _provider.FindForCheckoutAsync(flow);
        var tender = (invoice.Tenders ?? []).FirstOrDefault(entry => entry.ProviderKey == LoyaltyService.TenderKey);

        if (account is null || (tender is null && (account.Points <= 0 || invoice.InitialPaymentAmount is not > 0m)))
        {
            return null;
        }

        var settings = await _loyalty.GetSettingsAsync();

        return Initialize<CheckoutLoyaltyViewModel>("CheckoutLoyalty", model =>
        {
            model.AccountId = account.ItemId;
            model.ProgramName = settings.ProgramName;
            model.Currency = invoice.Currency;
            model.Points = account.Points;
            model.Value = LoyaltyService.GetValue(account.Points, settings, invoice.Currency);
            model.Used = tender?.Amount ?? 0m;
            model.IsApplied = tender is not null;
            model.PointsNeeded = account.Points < settings.MinimumPointsToRedeem ? settings.MinimumPointsToRedeem : null;
        }).Location("Aside:8");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!AcceptsPoints(flow))
        {
            return null;
        }

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + LoyaltyService.TenderKey + ":";

        if (string.Equals(action, ApplyAction, StringComparison.Ordinal) && await _provider.FindForCheckoutAsync(flow) is { Points: > 0 } account)
        {
            await _choices.AddAsync(flow, LoyaltyService.TenderKey, account.ItemId);
        }
        else if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            await _choices.RemoveAsync(flow, LoyaltyService.TenderKey, action[removePrefix.Length..]);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool AcceptsPoints(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false, OwnerId.Length: > 0 };
}
