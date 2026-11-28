using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Writes bank questions through their catalog entry handlers.
/// </summary>
public sealed class BankQuestionCatalogManager : CatalogManager<BankQuestion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankQuestionCatalogManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The catalog entry handlers.</param>
    /// <param name="logger">The logger.</param>
    public BankQuestionCatalogManager(
        BankQuestionStore store,
        IEnumerable<ICatalogEntryHandler<BankQuestion>> handlers,
        ILogger<CatalogManager<BankQuestion>> logger)
        : base(store, handlers, logger)
    {
    }
}
