using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public interface IAIProfileDocumentStore : ICatalog<AIProfileDocument>
{
    Task<IReadOnlyCollection<AIProfileDocument>> GetDocuments(string profileId);
}
