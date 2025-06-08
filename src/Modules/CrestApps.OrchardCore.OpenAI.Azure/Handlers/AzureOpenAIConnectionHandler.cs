using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;

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
        if (!string.Equals(context.Connection.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(AzureOpenAIConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always set the API key to an empty string during export to prevent accidental exposure.
        metadataNode[nameof(AzureOpenAIConnectionMetadata.ApiKey)] = string.Empty;

        context.ExportData["Properties"][nameof(AzureOpenAIConnectionMetadata)] = metadataNode;
    }

    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return;
        }

        var metadata = context.Connection.As<AzureOpenAIConnectionMetadata>();

        context.Values["Endpoint"] = metadata.Endpoint?.ToString();
        context.Values["AuthenticationType"] = metadata.AuthenticationType.ToString();

        if (!string.IsNullOrEmpty(metadata.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            context.Values["ApiKey"] = protector.Unprotect(metadata.ApiKey);
        }
    }
}
