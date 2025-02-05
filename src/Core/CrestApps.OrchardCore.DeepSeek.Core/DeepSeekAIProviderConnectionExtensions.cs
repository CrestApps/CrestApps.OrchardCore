using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public static class DeepSeekAIProviderConnectionExtensions
{
    public static string GetApiKey(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("ApiKey", throwException);

    public static string GetModel(this AIProviderConnection entry, bool throwException = true)
        => entry.GetStringValue("Model", throwException);
}
