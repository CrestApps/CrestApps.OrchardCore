using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Manages external tools.
/// </summary>
public sealed class ExternalToolManager : CatalogManager<ExternalTool>, IExternalToolManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalToolManager"/> class.
    /// </summary>
    /// <param name="store">The tool store.</param>
    /// <param name="handlers">The tool handlers.</param>
    /// <param name="logger">The logger.</param>
    public ExternalToolManager(
        IExternalToolStore store,
        IEnumerable<ICatalogEntryHandler<ExternalTool>> handlers,
        ILogger<CatalogManager<ExternalTool>> logger)
        : base(store, handlers, logger)
    {
    }
}
