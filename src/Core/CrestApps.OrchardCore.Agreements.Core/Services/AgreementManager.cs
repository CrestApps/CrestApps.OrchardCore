using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Manages agreements.
/// </summary>
public sealed class AgreementManager : CatalogManager<Agreement>, IAgreementManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public AgreementManager(AgreementStore store, IEnumerable<ICatalogEntryHandler<Agreement>> handlers, ILogger<CatalogManager<Agreement>> logger)
        : base(store, handlers, logger)
    {
    }
}
