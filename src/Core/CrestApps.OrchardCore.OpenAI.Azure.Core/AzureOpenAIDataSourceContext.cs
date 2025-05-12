using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public sealed class AzureOpenAIDataSourceContext
{
    public readonly AIProfile Profile;

    public AzureOpenAIDataSourceContext(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
    }
}
