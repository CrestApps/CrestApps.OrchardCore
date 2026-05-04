using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.OpenAI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.OpenAI.Handlers;

/// <summary>
/// Handles events for open AI provider connection.
/// </summary>
public sealed class OpenAIProviderConnectionHandler : IAIProviderConnectionHandler
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProviderConnectionHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public OpenAIProviderConnectionHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <summary>
    /// Performs the exporting operation.
    /// </summary>
    /// <param name="context">The context.</param>
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

    /// <summary>
    /// Initializes the ializing.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Initializing(InitializingAIProviderConnectionContext context)
    {
        if (!string.Equals(context.Connection.ClientName, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return;
        }

        if (!context.Connection.Has<OpenAIConnectionMetadata>())
        {
            return;
        }

        var metadata = context.Connection.GetOrCreate<OpenAIConnectionMetadata>();

        if (!string.IsNullOrEmpty(metadata.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            context.Values["ApiKey"] = protector.Unprotect(metadata.ApiKey);
        }

        context.Values["Endpoint"] = metadata.Endpoint?.ToString();
    }
}
