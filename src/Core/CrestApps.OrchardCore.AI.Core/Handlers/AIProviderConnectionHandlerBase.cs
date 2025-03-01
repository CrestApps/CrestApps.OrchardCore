using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AIProviderConnectionHandlerBase : IAIProviderConnectionHandler
{
    public virtual void Exporting(ExportingAIProviderConnectionContext context)
    {
    }

    public virtual void Initializing(InitializingAIProviderConnectionContext context)
    {
    }
}
