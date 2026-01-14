using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Handlers;

internal sealed class AzureOpenAIConnectionSettingsHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
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
        if (!string.Equals(context.Model.Source, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureOpenAIConnectionMetadata>();

        if (metadata.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(metadata.ApiKey))
        {
            context.Result.Fail(new ValidationResult(S["ApiKey is required when using ApiKey authentication."], [nameof(AzureOpenAIConnectionMetadata.ApiKey)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIProviderConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode = data[nameof(AIProviderConnection.Properties)]?[nameof(AzureOpenAIConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = connection.As<AzureOpenAIConnectionMetadata>();

        var endpoint = metadataNode[nameof(metadata.Endpoint)]?.GetValue<string>();

        if (endpoint is not null && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            metadata.Endpoint = uri;
        }

        metadata.AuthenticationType = metadataNode[nameof(metadata.AuthenticationType)]?.GetEnumValue<AzureAuthenticationType>()
            ?? AzureAuthenticationType.Default;

        var apiKey = metadataNode[nameof(metadata.ApiKey)]?.GetValue<string>();

        IDataProtector protector = null;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            protector ??= _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            metadata.ApiKey = protector.Protect(apiKey);
        }

        var speechAPIKey = metadataNode[nameof(metadata.SpeechAPIKey)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(speechAPIKey))
        {
            protector ??= _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            metadata.SpeechAPIKey = protector.Protect(speechAPIKey);
        }

        var speechRegion = metadataNode[nameof(metadata.SpeechRegion)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(speechRegion))
        {
            metadata.SpeechRegion = speechRegion;
        }

        connection.Put(metadata);

        return Task.CompletedTask;
    }
}
