using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Handlers;

public abstract class AIProviderConnectionHandlerBase : IAIProviderConnectionHandler
{
    public virtual void Exporting(ExportingAIProviderConnectionContext context)
    {
    }

    public virtual void Initializing(InitializingAIProviderConnectionContext context)
    {
    }
}
