using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AI.A2A.Handlers;

internal sealed class A2AConnectionSettingsHandler : CatalogEntryHandlerBase<A2AConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public A2AConnectionSettingsHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public override Task InitializingAsync(InitializingContext<A2AConnection> context)
        => ProtectSensitiveFieldsAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<A2AConnection> context)
        => ProtectSensitiveFieldsAsync(context.Model, context.Data);

    private Task ProtectSensitiveFieldsAsync(A2AConnection connection, JsonNode data)
    {
        var metadataNode = data[nameof(A2AConnection.Properties)]?[nameof(A2AConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var protector = _dataProtectionProvider.CreateProtector(A2AConstants.DataProtectionPurpose);
        var metadata = connection.GetOrCreate<A2AConnectionMetadata>();

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.ApiKey), val =>
        {
            metadata.ApiKey = val;
        });

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.BasicPassword), val =>
        {
            metadata.BasicPassword = val;
        });

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.OAuth2ClientSecret), val =>
        {
            metadata.OAuth2ClientSecret = val;
        });

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.OAuth2PrivateKey), val =>
        {
            metadata.OAuth2PrivateKey = val;
        });

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.OAuth2ClientCertificate), val =>
        {
            metadata.OAuth2ClientCertificate = val;
        });

        ProtectField(protector, metadataNode, nameof(A2AConnectionMetadata.OAuth2ClientCertificatePassword), val =>
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
