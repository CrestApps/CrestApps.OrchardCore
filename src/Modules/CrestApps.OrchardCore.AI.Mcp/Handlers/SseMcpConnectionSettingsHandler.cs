using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class SseMcpConnectionSettingsHandler : CatalogEntryHandlerBase<McpConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public SseMcpConnectionSettingsHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public override Task InitializingAsync(InitializingContext<McpConnection> context)
        => ProtectSensitiveFieldsAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<McpConnection> context)
        => ProtectSensitiveFieldsAsync(context.Model, context.Data);

    private Task ProtectSensitiveFieldsAsync(McpConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode = data[nameof(McpConnection.Properties)]?[nameof(SseMcpConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var protector = _dataProtectionProvider.CreateProtector(McpConstants.DataProtectionPurpose);
        var metadata = connection.As<SseMcpConnectionMetadata>();

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.ApiKey), val =>
        {
            metadata.ApiKey = val;
        });

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.BasicPassword), val =>
        {
            metadata.BasicPassword = val;
        });

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.OAuth2ClientSecret), val =>
        {
            metadata.OAuth2ClientSecret = val;
        });

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.OAuth2PrivateKey), val =>
        {
            metadata.OAuth2PrivateKey = val;
        });

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.OAuth2ClientCertificate), val =>
        {
            metadata.OAuth2ClientCertificate = val;
        });

        ProtectField(protector, metadataNode, nameof(SseMcpConnectionMetadata.OAuth2ClientCertificatePassword), val =>
        {
            metadata.OAuth2ClientCertificatePassword = val;
        });

        connection.Put(metadata);

        return Task.CompletedTask;
    }

    private static void ProtectField(IDataProtector protector, JsonObject node, string fieldName, Action<string> setter)
    {
        var value = node[fieldName]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(value))
        {
            setter(protector.Protect(value));
        }
    }
}
