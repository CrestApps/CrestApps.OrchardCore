using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Azure;
using CrestApps.Core.Azure.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Handlers;

internal sealed class AzureOpenAIConnectionSettingsHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
    private const string _legacyAzureConnectionMetadataPropertyName = "AzureOpenAIConnectionMetadata";

    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AzureOpenAIConnectionSettingsHandler(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureOpenAIConnectionHandler> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProviderConnection> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProviderConnection> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIProviderConnection> context)
    {
        if (!string.Equals(context.Model.Source, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.GetOrCreate<AzureConnectionMetadata>();

        if (metadata.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(metadata.ApiKey))
        {
            context.Result.Fail(new ValidationResult(S["ApiKey is required when using ApiKey authentication."], [nameof(AzureConnectionMetadata.ApiKey)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIProviderConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode =
            data[nameof(AIProviderConnection.Properties)]?[nameof(AzureConnectionMetadata)]?.AsObject() ??
            data[nameof(AIProviderConnection.Properties)]?[_legacyAzureConnectionMetadataPropertyName]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = connection.GetOrCreate<AzureConnectionMetadata>();

        var endpoint = metadataNode[nameof(metadata.Endpoint)]?.GetValue<string>();

        if (endpoint is not null && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            metadata.Endpoint = uri;
        }

        metadata.AuthenticationType = metadataNode[nameof(metadata.AuthenticationType)]?.GetEnumValue<AzureAuthenticationType>()
        ?? AzureAuthenticationType.Default;

        var identityId = metadataNode[nameof(metadata.IdentityId)]?.GetValue<string>()?.Trim();
        metadata.IdentityId = string.IsNullOrEmpty(identityId) ? null : identityId;

        var apiKey = metadataNode[nameof(metadata.ApiKey)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            metadata.ApiKey = protector.Protect(apiKey);
        }

        connection.Put(metadata);

        return Task.CompletedTask;
    }
}

