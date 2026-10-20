using System.Globalization;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Fundraising.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fundraising.Drivers;

/// <summary>
/// The Fundraising campaign part: its goal and amounts in the editor; on its page, progress toward the goal and the
/// donate form, whose fields post with Add to cart.
/// </summary>
internal sealed class FundraisingCampaignPartDisplayDriver : ContentPartDisplayDriver<FundraisingCampaignPart>
{
    private const string ProductActionDisplayType = "ProductAction";

    private readonly DonationService _donations;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public FundraisingCampaignPartDisplayDriver(DonationService donations, IHttpContextAccessor httpContextAccessor, IStringLocalizer<FundraisingCampaignPartDisplayDriver> stringLocalizer)
    {
        _donations = donations;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(FundraisingCampaignPart part, BuildPartDisplayContext context)
    {
        var progress = Initialize<CampaignProgressViewModel>("CampaignProgress", async model =>
        {
            model.Progress = await _donations.GetProgressAsync(part.ContentItem);
            model.Ended = part.EndsUtc < DateTime.UtcNow;
        });

        if (context.DisplayType == ProductActionDisplayType)
        {
            // The form is on the campaign's own page.
            if (_httpContextAccessor.HttpContext?.Request.RouteValues["contentItemId"] is not string shown || shown != part.ContentItem.ContentItemId)
            {
                return null;
            }

            return Combine(
                progress.Location(ProductActionDisplayType, "Actions:3"),
                Initialize<DonateFormViewModel>("DonateForm", async model => await BuildFormAsync(part, model)).Location(ProductActionDisplayType, "Actions:4"));
        }

        return progress.Location("Summary", "Meta:5");
    }

    public override IDisplayResult Edit(FundraisingCampaignPart part, BuildPartEditorContext context)
        => Initialize<FundraisingCampaignPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Goal = part.Goal == 0m ? null : part.Goal.ToString(CultureInfo.InvariantCulture);
            model.EndsUtc = part.EndsUtc;
            model.SuggestedAmounts = string.Join(", ", (part.SuggestedAmounts ?? []).Select(amount => amount.ToString(CultureInfo.InvariantCulture)));
            model.MinimumAmount = part.MinimumAmount.ToString(CultureInfo.InvariantCulture);
            model.TaxDeductible = part.TaxDeductible;
        });

    public override async Task<IDisplayResult> UpdateAsync(FundraisingCampaignPart part, UpdatePartEditorContext context)
    {
        var model = new FundraisingCampaignPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!TryParse(model.Goal, out var goal) || goal < 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Goal), S["Enter the goal as a number, such as 10000, or leave it empty for none."]);
        }

        if (!TryParse(model.MinimumAmount, out var minimum) || minimum < 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumAmount), S["Enter the least a donor can give, such as 5."]);
        }

        var amounts = new List<decimal>();

        foreach (var text in (model.SuggestedAmounts ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParse(text, out var amount) || amount <= 0m)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SuggestedAmounts), S["Enter the suggested amounts as numbers separated by commas, such as 25, 50, 100."]);

                break;
            }

            amounts.Add(amount);
        }

        part.Goal = goal;
        part.MinimumAmount = Math.Max(0.01m, minimum == 0m ? 1m : minimum);
        part.SuggestedAmounts = [.. amounts.Distinct().Order().Take(6)];
        part.EndsUtc = model.EndsUtc is { } ends ? DateTime.SpecifyKind(ends, DateTimeKind.Utc) : null;
        part.TaxDeductible = model.TaxDeductible;

        return Edit(part, context);
    }

    private async ValueTask BuildFormAsync(FundraisingCampaignPart part, DonateFormViewModel model)
    {
        var item = part.ContentItem;
        var currency = item.TryGet<ProductPart>(out var product) ? product.Currency : null;
        var shopperCurrency = _httpContextAccessor.HttpContext?.RequestServices.GetService<IShopperCurrency>();
        var shown = (shopperCurrency is null ? null : await shopperCurrency.GetCurrencyAsync()) ?? currency;
        var amounts = new List<(decimal Amount, string Label)>();

        // The amounts are offered in the shopper's currency, rounded to whole units.
        foreach (var amount in part.SuggestedAmounts ?? [])
        {
            var converted = shopperCurrency is null || string.IsNullOrEmpty(currency) ? amount : await shopperCurrency.ConvertAsync(amount, currency, shown, isPrice: false) ?? amount;
            var rounded = Math.Max(1m, Math.Round(converted, 0, MidpointRounding.AwayFromZero));

            amounts.Add((rounded, ProductPriceFormatter.Format(rounded, shown)));
        }

        model.FormId = "add-to-cart-" + item.ContentItemId;
        model.Amounts = [.. amounts.DistinctBy(entry => entry.Amount)];
        model.Currency = shown;
        model.Minimum = shopperCurrency is null || string.IsNullOrEmpty(currency)
            ? part.MinimumAmount
            : Math.Round(await shopperCurrency.ConvertAsync(part.MinimumAmount, currency, shown, isPrice: false) ?? part.MinimumAmount, 2, MidpointRounding.AwayFromZero);
        model.TaxDeductible = part.TaxDeductible;
        model.Ended = part.EndsUtc < DateTime.UtcNow;
    }

    private static bool TryParse(string value, out decimal amount)
    {
        amount = 0m;

        return string.IsNullOrWhiteSpace(value) || decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}

/// <summary>
/// Settings › Commerce › Fundraising: who receipts are from, and what they say.
/// </summary>
internal sealed class FundraisingSettingsDisplayDriver : SiteDisplayDriver<FundraisingSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public FundraisingSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => FundraisingConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, FundraisingSettings settings, BuildEditorContext context)
        => Initialize<FundraisingSettingsViewModel>("FundraisingSettings_Edit", model =>
        {
            model.OrganizationName = settings.OrganizationName;
            model.TaxId = settings.TaxId;
            model.ReceiptStatement = settings.ReceiptStatement;
            model.EmailReceipts = settings.EmailReceipts;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FundraisingPermissions.ManageFundraising))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, FundraisingSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FundraisingPermissions.ManageFundraising))
        {
            return null;
        }

        var model = new FundraisingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.OrganizationName = string.IsNullOrWhiteSpace(model.OrganizationName) ? null : model.OrganizationName.Trim();
        settings.TaxId = string.IsNullOrWhiteSpace(model.TaxId) ? null : model.TaxId.Trim();
        settings.ReceiptStatement = string.IsNullOrWhiteSpace(model.ReceiptStatement) ? null : model.ReceiptStatement.Trim();
        settings.EmailReceipts = model.EmailReceipts;

        return Edit(site, settings, context);
    }
}
