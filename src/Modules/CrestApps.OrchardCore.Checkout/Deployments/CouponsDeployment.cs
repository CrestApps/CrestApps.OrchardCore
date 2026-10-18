using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Checkout.Deployments;

/// <summary>
/// Exports every coupon, without how often it was used.
/// </summary>
public sealed class CouponsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CouponsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CouponsDeploymentStep>("Coupons");

    public CouponsDeploymentStep()
    {
        Name = CheckoutConstants.CouponsRecipeStep;
        Category = _category;
        Title = _title;
    }
}

internal sealed class CouponsDeploymentSource : DeploymentSourceBase<CouponsDeploymentStep>
{
    // How often a coupon was used, and its document, belong to the site it was used on.
    private static readonly string[] _environmentOwned = ["Id", "RedemptionCount", "CreatedUtc", "UpdatedUtc", "IsScoped", "HasItemLimits", "EffectiveTarget"];

    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ICouponStore _couponStore;

    public CouponsDeploymentSource(ICouponStore couponStore)
    {
        _couponStore = couponStore;
    }

    protected override async Task ProcessAsync(CouponsDeploymentStep step, DeploymentPlanResult result)
    {
        var coupons = new JsonArray();

        foreach (var coupon in (await _couponStore.GetAllAsync()).OrderBy(coupon => coupon.Code, StringComparer.OrdinalIgnoreCase))
        {
            var node = JsonSerializer.SerializeToNode(coupon, _options)!.AsObject();

            foreach (var member in _environmentOwned)
            {
                node.Remove(member);
            }

            coupons.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Coupons"] = coupons,
        });
    }
}

internal sealed class CouponsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, CouponsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(CouponsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("CouponsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("CouponsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(CouponsDeploymentStep step, BuildEditorContext context)
        => View("CouponsDeploymentStep_Fields_Edit", step).Location("Content");
}
