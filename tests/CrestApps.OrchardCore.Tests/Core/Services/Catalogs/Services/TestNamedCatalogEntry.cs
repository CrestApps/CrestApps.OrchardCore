using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

public sealed class TestNamedCatalogEntry : CatalogItem, INameAwareModel
{
    public string Name { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is not TestNamedCatalogEntry other)
        {
            return false;
        }

        return string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && GetType() == other.GetType();
    }

    public override int GetHashCode()
    {
        return (ItemId?.GetHashCode() ?? 0) ^ (Name?.GetHashCode() ?? 0) ^ GetType().GetHashCode();
    }
}

