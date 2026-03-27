using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AzureAIInference.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

internal sealed class AzureAIInferenceConnectionSettingsHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AzureAIInferenceConnectionSettingsHandler(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureAIInferenceConnectionHandler> stringLocalizer)
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
        if (!string.Equals(context.Model.Source, AzureAIInferenceConstants.ProviderName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureAIInferenceConnectionMetadata>();

        if (metadata.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(metadata.ApiKey))
        {
            context.Result.Fail(new ValidationResult(S["ApiKey is required when using ApiKey authentication."], [nameof(AzureAIInferenceConnectionMetadata.ApiKey)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIProviderConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, AzureAIInferenceConstants.ProviderName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode = data[nameof(AIProviderConnection.Properties)]?[nameof(AzureAIInferenceConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = connection.As<AzureAIInferenceConnectionMetadata>();

        metadata.AuthenticationType = metadataNode[nameof(metadata.AuthenticationType)]?.GetEnumValue<AzureAuthenticationType>() ?? AzureAuthenticationType.Default;

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
