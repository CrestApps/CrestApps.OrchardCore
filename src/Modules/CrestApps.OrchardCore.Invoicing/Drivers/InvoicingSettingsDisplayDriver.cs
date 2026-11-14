using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// Edits the invoicing settings under <b>Settings &gt; Commerce &gt; Invoicing</b>: what a new invoice starts with.
/// </summary>
public sealed class InvoicingSettingsDisplayDriver : SiteDisplayDriver<InvoicingSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPaymentTermManager _paymentTerms;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoicingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor for the signed-in user.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="paymentTerms">The payment terms offered as the default.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InvoicingSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IPaymentTermManager paymentTerms,
        IStringLocalizer<InvoicingSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _paymentTerms = paymentTerms;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => InvoicingConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, InvoicingSettings settings, BuildEditorContext context)
    {
        return Initialize<InvoicingSettingsViewModel>("InvoicingSettings_Edit", async model =>
        {
            model.DefaultPaymentTermId = settings.DefaultPaymentTermId;
            model.DefaultCurrency = settings.DefaultCurrency;
            model.DefaultNotes = settings.DefaultNotes;
            model.DefaultTerms = settings.DefaultTerms;
            model.DefaultFooter = settings.DefaultFooter;
            model.PricesIncludeTax = settings.PricesIncludeTax;
            model.AllowPartialPayment = settings.AllowPartialPayment;
            model.IssueOnSend = settings.IssueOnSend;
            model.CashRoundingIncrement = settings.CashRoundingIncrement;
            model.ShareLinkDays = settings.ShareLinkDays;
            model.PaymentTerms = [.. (await _paymentTerms.GetEnabledAsync()).Select(term => new SelectListItem(term.Name, term.ItemId))];
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageInvoicingSettings))
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, InvoicingSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageInvoicingSettings))
        {
            return null;
        }

        var model = new InvoicingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var currency = model.DefaultCurrency?.Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(currency) && (currency.Length != 3 || !currency.All(char.IsAsciiLetter)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultCurrency), S["The currency must be a three-letter code, such as USD."]);
        }

        if (model.CashRoundingIncrement is <= 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CashRoundingIncrement), S["The cash rounding increment must be more than zero, such as 0.05, or left empty."]);
        }

        if (model.ShareLinkDays is < 1 or > 3650)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ShareLinkDays), S["A share link must last from 1 to 3650 days."]);
        }

        settings.DefaultPaymentTermId = string.IsNullOrWhiteSpace(model.DefaultPaymentTermId) ? null : model.DefaultPaymentTermId;
        settings.DefaultCurrency = string.IsNullOrEmpty(currency) ? null : currency;
        settings.DefaultNotes = Trim(model.DefaultNotes);
        settings.DefaultTerms = Trim(model.DefaultTerms);
        settings.DefaultFooter = Trim(model.DefaultFooter);
        settings.PricesIncludeTax = model.PricesIncludeTax;
        settings.AllowPartialPayment = model.AllowPartialPayment;
        settings.IssueOnSend = model.IssueOnSend;
        settings.CashRoundingIncrement = model.CashRoundingIncrement;
        settings.ShareLinkDays = model.ShareLinkDays;

        return Edit(site, settings, context);
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
