using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// Generates unique codes that share a coupon's discount and rules, as the Generate codes form of Commerce › Coupons
/// does (<see cref="CouponEditor.CheckCodeGeneration"/>, then <see cref="ICouponStore.GenerateCodesAsync"/>). Narrower
/// than the catalog row: the codes are never returned, because each is a discount anyone holding it can spend and chat
/// transcripts are kept; the result gives how many were made and the link of the coupon page's Export action, which
/// downloads them as a CSV file (the review's "codes as a file"). It always asks for approval, because approval is fixed
/// when the tool is registered and every code shares the coupon's limits.
/// </summary>
internal sealed class GenerateCouponCodesTool : CouponAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "generateCouponCodes";

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
        },
        "count": {
          "type": "integer",
          "description": "How many codes to generate, 1 to 5,000."
        },
        "prefix": {
          "type": "string",
          "description": "What every code starts with: up to 12 letters, digits or dashes."
        },
        "usesPerCode": {
          "type": "integer",
          "description": "How many times each code can be used, more than zero; no limit when left out."
        }
      },
      "required": ["count"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Generates 1 to 5,000 unique codes that share a coupon's discount and rules, with an optional prefix and a use limit per code. The codes are never returned in the chat: the result gives how many were made and a link that downloads them as a CSV file. Not available for an automatic discount.";

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

        if (!TryReadInt(arguments, "count", out var count) || count is null || !TryReadInt(arguments, "usesPerCode", out var usesPerCode))
        {
            return "Nothing was generated: count (and usesPerCode, when given) must be whole numbers.";
        }

        var prefix = ReadString(arguments, "prefix")?.ToUpperInvariant();

        switch (CouponEditor.CheckCodeGeneration(coupon, count.Value, prefix, usesPerCode))
        {
            case CouponCodeGenerationProblem.Automatic:
                return "Nothing was generated: an automatic discount has no codes to generate.";
            case CouponCodeGenerationProblem.Count:
                return $"Nothing was generated: generate between 1 and {CheckoutConstants.MaxGeneratedCodes} codes at a time.";
            case CouponCodeGenerationProblem.Prefix:
                return "Nothing was generated: a prefix is up to 12 letters, digits or dashes.";
            case CouponCodeGenerationProblem.UsesPerCode:
                return "Nothing was generated: leave usesPerCode out for no limit, or give a number greater than zero.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var store = services.GetRequiredService<ICouponStore>();
        var codes = await store.GenerateCodesAsync(coupon, count.Value, prefix, usesPerCode, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        var (total, used) = await store.CountGeneratedCodesAsync(coupon.ItemId, cancellationToken);

        return Json(new
        {
            couponId = coupon.ItemId,
            coupon = coupon.Code,
            generated = codes.Count,
            prefix,
            usesPerCode,
            generatedCodes = new { total, used },
            adminPage = GetAdminPath(services, "CouponsEdit", new { itemId = coupon.ItemId }),
            codesFile = GetAdminPath(services, "CouponsExportCodes", new { itemId = coupon.ItemId }),
            message = $"Generated {codes.Count} codes for {coupon.Code}. They are not shown here: codesFile downloads them as a CSV file with their use counts (the Export action of the coupon's page).",
            warning = IsUnbounded(coupon) && usesPerCode is null
                ? "The coupon is enabled with neither a use limit nor an end date, and these codes have no use limit, so a leaked code keeps discounting."
                : null,
        });
    }
}
