using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDb;

public sealed class MongoDBAIProfileHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public MongoDBAIProfileHandler(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<MongoDBAIProfileHandler> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIDataSource> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIDataSource> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.ProviderName ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.MongoDB)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureMongoDBDataSourceMetadata>();

        if (metadata is null || string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata?.EndpointName))
        {
            context.Result.Fail(new ValidationResult(S["The endpoint name is required."], [nameof(metadata.EndpointName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata?.CollectionName))
        {
            context.Result.Fail(new ValidationResult(S["The collection name is required."], [nameof(metadata.CollectionName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata?.AppName))
        {
            context.Result.Fail(new ValidationResult(S["The app name is required."], [nameof(metadata.AppName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata?.Authentication?.Username))
        {
            context.Result.Fail(new ValidationResult(S["The username is required."], [nameof(AzureAIProfileMongoDBAuthenticationType.Username)]));
        }

        if (string.IsNullOrWhiteSpace(metadata?.Authentication?.Password))
        {
            context.Result.Fail(new ValidationResult(S["The password is required."], [nameof(AzureAIProfileMongoDBAuthenticationType.Password)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(AIDataSource source, JsonNode data)
    {
        if (source.ProfileSource != AzureOpenAIConstants.ProviderName ||
            source.Type != AzureOpenAIConstants.DataSourceTypes.MongoDB)
        {
            return Task.CompletedTask;
        }

        var metadataNode = data[nameof(AIProfile.Properties)]?[nameof(AzureMongoDBDataSourceMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = source.As<AzureMongoDBDataSourceMetadata>();

        metadata.Authentication ??= new AzureAIProfileMongoDBAuthenticationType();

        var endpointName = metadataNode[nameof(metadata.EndpointName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(endpointName))
        {
            metadata.EndpointName = endpointName;
        }

        var indexName = metadataNode[nameof(metadata.IndexName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(indexName))
        {
            metadata.IndexName = indexName;
        }

        var datbaseName = metadataNode[nameof(metadata.DatabaseName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(datbaseName))
        {
            metadata.DatabaseName = datbaseName;
        }

        var collectionName = metadataNode[nameof(metadata.CollectionName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(collectionName))
        {
            metadata.CollectionName = collectionName;
        }

        var appName = metadataNode[nameof(metadata.AppName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(appName))
        {
            metadata.AppName = appName;
        }

        var authentication = metadataNode[nameof(metadata.Authentication)]?.AsObject();

        var username = authentication?[nameof(metadata.Authentication.Username)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(username))
        {
            metadata.Authentication.Username = username;
        }

        var password = authentication?[nameof(metadata.Authentication.Password)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(password))
        {
            var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

            metadata.Authentication.Password = protector.Protect(password);
        }

        source.Put(metadata);

        return Task.CompletedTask;
    }
}
