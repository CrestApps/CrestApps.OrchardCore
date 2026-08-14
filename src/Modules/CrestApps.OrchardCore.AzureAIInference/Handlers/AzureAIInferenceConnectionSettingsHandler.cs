using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.AzureAIInference;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Azure.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AzureAIInference.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

internal sealed class AzureAIInferenceConnectionSettingsHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAIInferenceConnectionSettingsHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AzureAIInferenceConnectionSettingsHandler(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureAIInferenceConnectionHandler> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(context.Model.Source, AzureAIInferenceConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.GetOrCreate<AzureAIInferenceConnectionMetadata>();

        if (metadata.Endpoint is null)
        {
            context.Result.Fail(new ValidationResult(S["Endpoint is required for Azure AI Inference connections."], [nameof(AzureAIInferenceConnectionMetadata.Endpoint)]));
        }

        if (metadata.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(metadata.ApiKey))
        {
            context.Result.Fail(new ValidationResult(S["ApiKey is required when using ApiKey authentication."], [nameof(AzureAIInferenceConnectionMetadata.ApiKey)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIProviderConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, AzureAIInferenceConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode = GetMetadataNode(data);

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = connection.GetOrCreate<AzureAIInferenceConnectionMetadata>();

        var endpoint = metadataNode[nameof(metadata.Endpoint)]?.GetValue<string>();

        if (endpoint is not null && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            metadata.Endpoint = uri;
        }

        metadata.AuthenticationType = metadataNode[nameof(metadata.AuthenticationType)]?.GetEnumValue<AzureAuthenticationType>() ?? AzureAuthenticationType.Default;

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

    private static JsonObject GetMetadataNode(JsonNode data)
    {
        JsonObject result = [];

        CopyNode(data, result, nameof(AzureAIInferenceConnectionMetadata.Endpoint));
        CopyNode(data, result, nameof(AzureAIInferenceConnectionMetadata.AuthenticationType));
        CopyNode(data, result, nameof(AzureAIInferenceConnectionMetadata.ApiKey));
        CopyNode(data, result, nameof(AzureAIInferenceConnectionMetadata.IdentityId));

        var nested = data[nameof(AIProviderConnection.Properties)]?[nameof(AzureAIInferenceConnectionMetadata)]?.AsObject();

        if (nested != null)
        {
            foreach (var property in nested)
            {
                result[property.Key] = property.Value?.DeepClone();
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static void CopyNode(JsonNode source, JsonObject destination, string propertyName)
    {
        if (source[propertyName] is JsonNode node)
        {
            destination[propertyName] = node.DeepClone();
        }
    }
}
