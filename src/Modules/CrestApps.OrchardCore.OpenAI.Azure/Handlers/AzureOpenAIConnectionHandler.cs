using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Azure;
using CrestApps.Core.Azure.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.OpenAI.Azure.Handlers;

public sealed class AzureOpenAIConnectionHandler : IAIProviderConnectionHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AzureOpenAIConnectionHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public void Exporting(ExportingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(AzureConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always set the API key to an empty string during export to prevent accidental exposure.
        metadataNode[nameof(AzureConnectionMetadata.ApiKey)] = string.Empty;

        context.ExportData["Properties"][nameof(AzureConnectionMetadata)] = metadataNode;
    }

    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        if (!context.Connection.Has<AzureConnectionMetadata>())
        {
            return;
        }

        var metadata = context.Connection.GetOrCreate<AzureConnectionMetadata>();

        context.Values["Endpoint"] = metadata.Endpoint?.ToString();
        context.Values["AuthenticationType"] = metadata.AuthenticationType.ToString();
        context.Values["IdentityId"] = metadata.IdentityId;

        if (!string.IsNullOrEmpty(metadata.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            context.Values["ApiKey"] = protector.Unprotect(metadata.ApiKey);
        }
    }
}
