using Azure.Core;
using Azure.Identity;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureArmOptions
{
    public string SubscriptionId { get; set; }

    public string TenantId { get; set; }

    public string ClientId { get; set; }

    public string ClientSecret { get; set; }

    public TokenCredential GetTokenCredential()
    {
        if (!string.IsNullOrEmpty(TenantId) &&
            !string.IsNullOrEmpty(ClientId) &&
            !string.IsNullOrEmpty(ClientSecret))
        {
            return new ClientSecretCredential(TenantId, ClientId, ClientSecret);
        }

        return new DefaultAzureCredential();
    }
}

