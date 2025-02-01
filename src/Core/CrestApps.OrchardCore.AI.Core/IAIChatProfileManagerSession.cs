using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIChatProfileManagerSession
{
    void Store(AIChatProfile profile);

    bool Forget(string id);

    bool Recall(string id, out AIChatProfile profile);

    void Clear();
}
