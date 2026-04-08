using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.OpenAI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.Core;

namespace CrestApps.OrchardCore.OpenAI.Handlers;

public sealed class OpenAIProviderConnectionHandler : IAIProviderConnectionHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public OpenAIProviderConnectionHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public void Exporting(ExportingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(OpenAIConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always set the API key to an empty string during export to prevent accidental exposure.
        metadataNode[nameof(OpenAIConnectionMetadata.ApiKey)] = string.Empty;

        context.ExportData["Properties"][nameof(OpenAIConnectionMetadata)] = metadataNode;
    }

    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        var metadata = context.Connection.As<OpenAIConnectionMetadata>();

        if (!string.IsNullOrEmpty(metadata.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            context.Values["ApiKey"] = protector.Unprotect(metadata.ApiKey);
        }

        context.Values["Endpoint"] = metadata.Endpoint?.ToString();
    }
}
