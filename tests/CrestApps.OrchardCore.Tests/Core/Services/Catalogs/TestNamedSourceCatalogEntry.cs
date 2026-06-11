using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services.Catalogs;

internal sealed class TestNamedSourceCatalogEntry : CatalogItem, INameAwareModel, ISourceAwareModel
{
    public string Name { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
