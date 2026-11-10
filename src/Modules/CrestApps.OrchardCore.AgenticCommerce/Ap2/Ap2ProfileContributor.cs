using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;

namespace CrestApps.OrchardCore.AgenticCommerce.Ap2;

/// <summary>
/// Offers UCP's AP2 mandates extension: checkouts signed by the store, completed only with a mandate the buyer signed.
/// </summary>
public sealed class Ap2ProfileContributor : IUcpProfileContributor
{
    /// <summary>
    /// Returns the extension's name in a UCP version: 2026-08-25 moved it under <c>dev.ucp.common.payment</c>.
    /// </summary>
    /// <param name="version">The UCP version.</param>
    public static string GetName(string version)
        => version == AgenticCommerceConstants.Versions.Ucp20260408 ? UcpConstants.Capabilities.Ap2MandateLegacy : UcpConstants.Capabilities.Ap2Mandate;

    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Capabilities.Add(new UcpCapability
        {
            Name = GetName(context.Version),
            Version = context.Version,
            Spec = UcpConstants.Url(context.Version, "specification/payment/extensions/ap2-mandates"),
            Schema = UcpConstants.Url(context.Version, "schemas/common/payment_ap2_mandate.json"),
            Extends = [UcpConstants.Capabilities.Checkout],
            Config = new JsonObject
            {
                ["vp_formats_supported"] = new JsonObject
                {
                    ["dc+sd-jwt"] = new JsonObject(),
                },
            },
        });

        return Task.CompletedTask;
    }
}
