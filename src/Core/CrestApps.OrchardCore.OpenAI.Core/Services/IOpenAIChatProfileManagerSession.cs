using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public interface IOpenAIChatProfileManagerSession
{
    void Store(OpenAIChatProfile profile);

    bool Forget(string id);

    bool Recall(string id, out OpenAIChatProfile profile);

    void Clear();
}
