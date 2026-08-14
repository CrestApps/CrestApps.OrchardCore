using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Azure;
using CrestApps.Core.Azure.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.OpenAI.Azure.Handlers;

/// <summary>
/// Handles events for azure open AI connection.
/// </summary>
public sealed class AzureOpenAIConnectionHandler : IAIProviderConnectionHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIConnectionHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public AzureOpenAIConnectionHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Performs the exporting operation.
    /// </summary>
    /// <param name="context">The context.</param>
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

    /// <summary>
    /// Initializes the ializing.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        if (!context.Connection.TryGet<AzureConnectionMetadata>(out var metadata))
        {
            return;
        }

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
