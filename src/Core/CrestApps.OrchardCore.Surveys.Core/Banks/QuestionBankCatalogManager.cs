using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Writes question banks through their catalog entry handlers.
/// </summary>
public sealed class QuestionBankCatalogManager : CatalogManager<QuestionBank>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankCatalogManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The catalog entry handlers.</param>
    /// <param name="logger">The logger.</param>
    public QuestionBankCatalogManager(
        QuestionBankStore store,
        IEnumerable<ICatalogEntryHandler<QuestionBank>> handlers,
        ILogger<CatalogManager<QuestionBank>> logger)
        : base(store, handlers, logger)
    {
    }
}
