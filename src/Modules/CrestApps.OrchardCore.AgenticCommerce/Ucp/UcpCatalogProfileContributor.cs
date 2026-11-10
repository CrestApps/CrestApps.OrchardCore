namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Offers UCP's catalog search and lookup and the cart.
/// </summary>
public sealed class UcpCatalogProfileContributor : IUcpProfileContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (name, page, schema) in new[]
        {
            (UcpConstants.Capabilities.CatalogSearch, "specification/shopping/catalog", "schemas/shopping/catalog_search.json"),
            (UcpConstants.Capabilities.CatalogLookup, "specification/shopping/catalog", "schemas/shopping/catalog_lookup.json"),
            (UcpConstants.Capabilities.Cart, "specification/shopping/cart", "schemas/shopping/cart.json"),
        })
        {
            context.Capabilities.Add(new UcpCapability
            {
                Name = name,
                Version = context.Version,
                Spec = UcpConstants.Url(context.Version, page),
                Schema = UcpConstants.Url(context.Version, schema),
            });
        }

        return Task.CompletedTask;
    }
}
