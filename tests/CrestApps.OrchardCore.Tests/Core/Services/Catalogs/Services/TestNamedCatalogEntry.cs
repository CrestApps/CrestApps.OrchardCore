using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

public sealed class TestNamedCatalogEntry : CatalogEntry, INameAwareModel
{
    public string Name { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is not TestNamedCatalogEntry other)
        {
            return false;
        }

        return string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && GetType() == other.GetType();
    }

    public override int GetHashCode()
    {
        return (Id?.GetHashCode() ?? 0) ^ (Name?.GetHashCode() ?? 0) ^ GetType().GetHashCode();
    }
}

