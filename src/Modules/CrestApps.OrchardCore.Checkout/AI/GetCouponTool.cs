using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// Returns one coupon as its editor on Commerce › Coupons shows it: all its rules, its recent uses with what each paid
/// for (<see cref="ICheckoutReferenceDescriber"/>), the uses held by open checkouts, how many codes were generated for it
/// and used, and its discount link. Narrower than the catalog row: the generated codes themselves are never returned,
/// because each is a discount anyone holding it can spend; the result links to the editor's download of them. A use is
/// described by its amount, status and what it paid for, without the buyer or the code typed.
/// </summary>
internal sealed class GetCouponTool : CouponAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getCoupon";

    private const int RecentUses = 25;

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
          "description": "The coupon's own code, such as SPRING15, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one coupon by id or code with all its rules, its recent uses with what each paid for, uses currently held by open checkouts, the count of generated codes and how many were used, and its discount link. The generated codes themselves are never returned; the result links to the coupon's page, where they are downloaded.";

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

        var redemptions = services.GetRequiredService<ICouponRedemptionService>();
        var store = services.GetRequiredService<ICouponStore>();
        var describers = services.GetServices<ICheckoutReferenceDescriber>().ToArray();
        var uses = new List<object>();
        var references = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var redemption in await redemptions.ListAsync(coupon.Code, RecentUses, cancellationToken))
        {
            string paidFor = null;

            if (!string.IsNullOrEmpty(redemption.ReferenceId))
            {
                var key = redemption.ReferenceType + "|" + redemption.ReferenceId;

                if (!references.TryGetValue(key, out paidFor))
                {
                    // What each use paid for, such as an order, reads by its number rather than its id when a module knows it.
                    foreach (var describer in describers)
                    {
                        if (await describer.DescribeAsync(redemption.ReferenceType, redemption.ReferenceId, cancellationToken) is { } description)
                        {
                            paidFor = description.Text;

                            break;
                        }
                    }

                    paidFor ??= $"{redemption.ReferenceType} {redemption.ReferenceId}";
                    references[key] = paidFor;
                }
            }

            uses.Add(new
            {
                status = ToCamel(redemption.Status.ToString()),
                amount = redemption.Amount,
                currency = redemption.Currency,
                paidFor,
                when = await LocalTimeAsync(services, redemption.RedeemedUtc ?? redemption.CreatedUtc),
            });
        }

        var (generated, usedGenerated) = await store.CountGeneratedCodesAsync(coupon.ItemId, cancellationToken);
        var request = services.GetService<IHttpContextAccessor>()?.HttpContext?.Request;

        return Json(new
        {
            coupon = await DescribeAsync(services, coupon, services.GetRequiredService<IClock>().UtcNow),
            recentUses = uses,
            heldByOpenCheckouts = await redemptions.CountHeldAsync(coupon.Code, cancellationToken),
            generatedCodes = new
            {
                total = generated,
                used = usedGenerated,
                note = generated > 0 ? "The codes are not shown here. Download them with their use counts from the coupon's page." : null,
            },
            discountLink = coupon.CanBeTyped && request is not null
                ? $"{request.Scheme}://{request.Host}{request.PathBase}/discount/{Uri.EscapeDataString(coupon.Code)}"
                : null,
            adminPage = GetAdminPath(services, "CouponsEdit", new { itemId = coupon.ItemId }),
        });
    }
}
