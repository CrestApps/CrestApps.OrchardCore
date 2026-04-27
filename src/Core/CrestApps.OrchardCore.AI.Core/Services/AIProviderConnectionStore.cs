using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the AI provider connection store.
/// </summary>
public sealed class AIProviderConnectionStore : NamedSourceCatalog<AIProviderConnection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager.</param>
    public AIProviderConnectionStore(IDocumentManager<DictionaryDocument<AIProviderConnection>> documentManager)
        : base(documentManager)
    {
    }
}
