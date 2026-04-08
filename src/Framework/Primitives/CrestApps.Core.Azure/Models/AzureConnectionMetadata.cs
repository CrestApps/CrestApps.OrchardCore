namespace CrestApps.Core.Azure.Models;

public class AzureConnectionMetadata
{
    public Uri Endpoint { get; set; }

    public AzureAuthenticationType AuthenticationType { get; set; }

    public string ApiKey { get; set; }

    public string IdentityId { get; set; }
}
