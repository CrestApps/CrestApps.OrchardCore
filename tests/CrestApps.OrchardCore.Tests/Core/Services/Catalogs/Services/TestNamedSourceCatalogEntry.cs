using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;

internal sealed class TestNamedSourceCatalogEntry : CatalogEntry, INameAwareModel, ISourceAwareModel
{
    public string Name { get; set; }
    public string Source { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is not TestNamedSourceCatalogEntry other)
        {
            return false;
        }

        return string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Source, other.Source, StringComparison.Ordinal)
            && GetType() == other.GetType();
    }

    public override int GetHashCode()
    {
        return (Id?.GetHashCode() ?? 0)
            ^ (Name?.GetHashCode() ?? 0)
            ^ (Source?.GetHashCode() ?? 0)
            ^ GetType().GetHashCode();
    }
}
