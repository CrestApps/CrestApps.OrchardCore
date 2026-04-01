using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using CrestApps.AI.OpenAI;
using CrestApps.AI.OpenAI.Models;
using CrestApps.Handlers;
using CrestApps.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Handlers;

internal sealed class OpenAIProviderConnectionSettingsHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public OpenAIProviderConnectionSettingsHandler(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<OpenAIProviderConnectionHandler> stringLocalizer)
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
        if (!string.Equals(context.Model.Source, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<OpenAIConnectionMetadata>();

        if (metadata.Endpoint is null)
        {
            context.Result.Fail(new ValidationResult(S["No or invalid endpoint value given the OpenAI connection."], [nameof(OpenAIConnectionMetadata.Endpoint)]));
        }

        if (string.IsNullOrEmpty(metadata.ApiKey))
        {
            context.Result.Fail(new ValidationResult(S["ApiKey is required for OpenAI connection."], [nameof(OpenAIConnectionMetadata.ApiKey)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIProviderConnection connection, JsonNode data)
    {
        if (!string.Equals(connection.Source, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var metadataNode = data[nameof(AIProviderConnection.Properties)]?[nameof(OpenAIConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = connection.As<OpenAIConnectionMetadata>();

        var endpoint = metadataNode[nameof(metadata.Endpoint)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            metadata.Endpoint = uri;
        }

        var apiKey = metadataNode[nameof(metadata.ApiKey)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            metadata.ApiKey = protector.Protect(apiKey);
        }

        connection.Put(metadata);

        return Task.CompletedTask;
    }
}
