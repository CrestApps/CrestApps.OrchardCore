using CrestApps.Azure.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureOpenAIConnectionMetadata : AzureConnectionMetadata
{
    public bool EnableLogging { get; set; }
}
