namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Offers the REST binding with checkout, its fulfillment, discount and buyer consent extensions, and orders.
/// </summary>
public sealed class UcpShoppingProfileContributor : IUcpProfileContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var version = context.Version;

        context.Services.Add(new UcpService
        {
            Transport = "rest",
            Schema = UcpConstants.Url(version, "services/shopping/rest.openapi.json"),
            Endpoint = $"{context.BaseUrl}/{UcpConstants.RoutePrefix}",
        });

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.Checkout,
            Version = version,
            Spec = UcpConstants.Url(version, "specification/shopping/checkout"),
            Schema = UcpConstants.Url(version, "schemas/shopping/checkout.json"),
        });

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.Fulfillment,
            Version = version,
            Spec = UcpConstants.Url(version, "specification/shopping/extensions/fulfillment"),
            Schema = UcpConstants.Url(version, "schemas/shopping/fulfillment.json"),
            Extends = [UcpConstants.Capabilities.Checkout],
        });

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.Discount,
            Version = version,
            Spec = UcpConstants.Url(version, "specification/shopping/extensions/discount"),
            Schema = UcpConstants.Url(version, "schemas/shopping/discount.json"),
            Extends = [UcpConstants.Capabilities.Checkout],
        });

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.BuyerConsent,
            Version = version,
            Spec = UcpConstants.Url(version, "specification/shopping/extensions/buyer-consent"),
            Schema = UcpConstants.Url(version, "schemas/shopping/buyer_consent.json"),
            Extends = [UcpConstants.Capabilities.Checkout],
        });

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.Order,
            Version = version,
            Spec = UcpConstants.Url(version, "specification/shopping/order"),
            Schema = UcpConstants.Url(version, "schemas/shopping/order.json"),
        });

        return Task.CompletedTask;
    }
}
