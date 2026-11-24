using CrestApps.Core.AI;
using CrestApps.OrchardCore.Checkout.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the coupon AI tools whenever the Checkout and AI features are on. Every tool needs <c>ManageCoupons</c>,
/// as Commerce › Coupons. Saving a coupon, generating codes and deleting a coupon ask for approval; generated codes are
/// never returned.
/// </summary>
[Feature(CheckoutConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CheckoutToolScope>();

        services.AddCoreAITool<ListCouponsTool>(ListCouponsTool.TheName)
            .WithTitle(S["List Coupons"])
            .WithDescription(S["Lists coupons and automatic discounts with code, description, what they take off, dates, use limit and uses so far, and whether each can be used now. Filters by text and by active, not available or disabled, with paging; it also flags enabled coupons that have neither a use limit nor an end date."])
            .WithCategory(S["Promotions"])
            .Selectable();

        services.AddCoreAITool<GetCouponTool>(GetCouponTool.TheName)
            .WithTitle(S["Get Coupon"])
            .WithDescription(S["Returns one coupon by id or code with all its rules, its recent uses with what each paid for, uses currently held by open checkouts, the count of generated codes and how many were used, and its discount link. The generated codes themselves are never returned in the chat; the result gives a link that downloads them as a CSV file."])
            .WithCategory(S["Promotions"])
            .Selectable();

        services.AddCoreAITool<SaveCouponTool>(SaveCouponTool.TheName)
            .WithTitle(S["Save Coupon"])
            .WithDescription(S["Creates or changes a coupon or automatic discount: code, percentage or fixed amount, what it applies to, dates in the site's time zone, use limits, spend and quantity conditions, buy X get Y, product, category, customer and channel limits, and what it combines with. It is checked with the same rules as the coupon editor and never changes how often a coupon was used."])
            .WithCategory(S["Promotions"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteCouponTool>(DeleteCouponTool.TheName)
            .WithTitle(S["Delete Coupon"])
            .WithDescription(S["Deletes a coupon together with all its generated codes. Orders that already used it keep their discount."])
            .WithCategory(S["Promotions"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<GenerateCouponCodesTool>(GenerateCouponCodesTool.TheName)
            .WithTitle(S["Generate Coupon Codes"])
            .WithDescription(S["Generates 1 to 5,000 unique codes that share a coupon's discount and rules, with an optional prefix and a use limit per code. The codes are never returned in the chat: the result gives how many were made and a link that downloads them as a CSV file. Not available for an automatic discount."])
            .WithCategory(S["Promotions"])
            .RequireApproval()
            .Selectable();
    }
}
