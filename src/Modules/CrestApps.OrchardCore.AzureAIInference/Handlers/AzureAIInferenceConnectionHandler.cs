using CrestApps.Core;
using CrestApps.Core.AI.AzureAIInference;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AzureAIInference.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

/// <summary>
/// Handles events for azure AI inference connection.
/// </summary>
public sealed class AzureAIInferenceConnectionHandler : IAIProviderConnectionHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAIInferenceConnectionHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public AzureAIInferenceConnectionHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Performs the exporting operation.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Exporting(ExportingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, AzureAIInferenceConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(AzureAIInferenceConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always set the API key to an empty string during export to prevent accidental exposure.
        metadataNode[nameof(AzureAIInferenceConnectionMetadata.ApiKey)] = string.Empty;

        context.ExportData["Properties"][nameof(AzureAIInferenceConnectionMetadata)] = metadataNode;
    }

    /// <summary>
    /// Initializes the ializing.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, AzureAIInferenceConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        if (!context.Connection.Has<AzureAIInferenceConnectionMetadata>())
        {
            return;
        }

        var metadata = context.Connection.GetOrCreate<AzureAIInferenceConnectionMetadata>();

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
