using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public interface IAIDocumentStore : ICatalog<AIDocument>
{
    Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType);
}
