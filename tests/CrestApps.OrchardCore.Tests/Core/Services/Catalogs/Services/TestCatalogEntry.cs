using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

public sealed class TestCatalogEntry : CatalogEntry
{
    public override bool Equals(object obj)
    {
        if (obj is not TestCatalogEntry other)
        {
            return false;
        }

        return string.Equals(Id, other.Id, StringComparison.Ordinal)
            && GetType() == other.GetType();
    }

    public override int GetHashCode()
    {
        return (Id?.GetHashCode() ?? 0) ^ GetType().GetHashCode();
    }
}

