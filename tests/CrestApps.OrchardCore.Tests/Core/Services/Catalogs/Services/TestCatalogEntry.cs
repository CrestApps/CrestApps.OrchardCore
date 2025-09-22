using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

public sealed class TestCatalogEntry : CatalogItem
{
    public override bool Equals(object obj)
    {
        if (obj is not TestCatalogEntry other)
        {
            return false;
        }

        return string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
            && GetType() == other.GetType();
    }

    public override int GetHashCode()
    {
        return (ItemId?.GetHashCode() ?? 0) ^ GetType().GetHashCode();
    }
}

