using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// Deletes a coupon together with all its generated codes, as the Delete action of Commerce › Coupons does. Orders that
/// already used it keep their discount. It asks for approval.
/// </summary>
internal sealed class DeleteCouponTool : CouponAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteCoupon";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "couponId": {
          "type": "string",
          "description": "The coupon's id, from listCoupons."
        },
        "code": {
          "type": "string",
          "description": "The coupon's own code, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a coupon together with all its generated codes. Orders that already used it keep their discount.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (coupon, error) = await FindCouponAsync(services, ReadString(arguments, "couponId"), ReadString(arguments, "code"), cancellationToken);

        if (coupon is null)
        {
            return error;
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var store = services.GetRequiredService<ICouponStore>();
        var (generated, _) = await store.CountGeneratedCodesAsync(coupon.ItemId, cancellationToken);

        await store.DeleteGeneratedCodesAsync(coupon.ItemId, cancellationToken);
        await store.DeleteAsync(coupon, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            deleted = new
            {
                couponId = coupon.ItemId,
                code = coupon.Code,
                generatedCodes = generated,
            },
            message = generated > 0
                ? $"Coupon {coupon.Code} and its {generated} generated codes were deleted. Orders that used it keep their discount."
                : $"Coupon {coupon.Code} was deleted. Orders that used it keep their discount.",
        });
    }
}
