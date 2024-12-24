using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IAIChatProfileManager
{
    Task DeleteAsync(AIChatProfile profile);

    Task<AIChatProfile> FindByIdAsync(string id);

    Task<AIChatProfile> NewAsync(string source, JsonNode data = null);

    Task<AIProfileResult> PageQueriesAsync(int page, int pageSize, QueryContext context);

    Task SaveAsync(AIChatProfile profile);
}
