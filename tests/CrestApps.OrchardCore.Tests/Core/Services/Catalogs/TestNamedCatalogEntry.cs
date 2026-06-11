using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

internal sealed class TestNamedCatalogEntry : CatalogItem, INameAwareModel
{
    public string Name { get; set; } = string.Empty;
}
