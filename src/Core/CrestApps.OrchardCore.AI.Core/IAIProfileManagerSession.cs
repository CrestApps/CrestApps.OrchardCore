using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

public interface IAIProfileManagerSession
{
    void Store(AIProfile profile);

    bool Forget(string id);

    bool Recall(string id, out AIProfile profile);

    void Clear();
}
