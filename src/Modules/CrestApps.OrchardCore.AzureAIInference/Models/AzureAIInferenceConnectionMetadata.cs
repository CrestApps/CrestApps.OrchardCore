namespace CrestApps.OrchardCore.AzureAIInference.Models;

public sealed class AzureAIInferenceConnectionMetadata
{
    public AzureAuthenticationType AuthenticationType { get; set; }

    public string ApiKey { get; set; }
}
