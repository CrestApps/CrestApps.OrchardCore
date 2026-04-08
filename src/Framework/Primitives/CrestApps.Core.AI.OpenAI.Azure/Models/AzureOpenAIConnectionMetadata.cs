using CrestApps.Core.Azure.Models;

namespace CrestApps.Core.AI.OpenAI.Azure.Models;

public class AzureOpenAIConnectionMetadata : AzureConnectionMetadata
{
    public bool EnableLogging { get; set; }
}
