namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureFunctionCall
{
    public string Name { get; set; }

    public string Arguments { get; set; }
}
