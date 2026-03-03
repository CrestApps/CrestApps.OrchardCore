namespace CrestApps.AI.Models;

public interface IAIProviderConnectionHandler
{
    void Initializing(InitializingAIProviderConnectionContext context);

    void Exporting(ExportingAIProviderConnectionContext context);
}
