using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

public interface IAIDocumentStore : ICatalog<AIDocument>
{
    Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType);
}
