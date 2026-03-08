using CrestApps.Azure.Core.Models;

namespace CrestApps.AI.OpenAI.Azure.Models;

public class AzureOpenAIConnectionMetadata : AzureConnectionMetadata
{
    public bool EnableLogging { get; set; }
}
