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

        var metadata = GetMongoDBMetadata(context.Model);

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.EndpointName))
        {
            context.Result.Fail(new ValidationResult(S["The endpoint name is required."], [nameof(metadata.EndpointName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.CollectionName))
        {
            context.Result.Fail(new ValidationResult(S["The collection name is required."], [nameof(metadata.CollectionName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.AppName))
        {
            context.Result.Fail(new ValidationResult(S["The app name is required."], [nameof(metadata.AppName)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.Authentication?.Username))
        {
            context.Result.Fail(new ValidationResult(S["The username is required."], [nameof(AzureAIProfileMongoDBAuthenticationType.Username)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.Authentication?.Password))
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

        // Try the new metadata format first
        var metadataNode = data[nameof(AIProfile.Properties)]?[nameof(AzureMongoDBDataSourceMetadata)]?.AsObject();

        // Fall back to legacy metadata format
#pragma warning disable CS0618 // Type or member is obsolete
        metadataNode ??= data[nameof(AIProfile.Properties)]?[nameof(AzureAIProfileMongoDBMetadata)]?.AsObject();
#pragma warning restore CS0618 // Type or member is obsolete

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

    /// <summary>
    /// Gets MongoDB metadata from the data source, trying new metadata first, then falling back to legacy.
    /// </summary>
    private static AzureMongoDBDataSourceMetadata GetMongoDBMetadata(AIDataSource dataSource)
    {
        // Try new metadata first
        var newMetadata = dataSource.As<AzureMongoDBDataSourceMetadata>();
        if (newMetadata is not null && !string.IsNullOrWhiteSpace(newMetadata.IndexName))
        {
            return newMetadata;
        }

        // Fall back to legacy metadata
#pragma warning disable CS0618 // Type or member is obsolete
        var legacyMetadata = dataSource.As<AzureAIProfileMongoDBMetadata>();
        if (legacyMetadata is not null && !string.IsNullOrWhiteSpace(legacyMetadata.IndexName))
        {
            return new AzureMongoDBDataSourceMetadata
            {
                IndexName = legacyMetadata.IndexName,
                EndpointName = legacyMetadata.EndpointName,
                AppName = legacyMetadata.AppName,
                CollectionName = legacyMetadata.CollectionName,
                DatabaseName = legacyMetadata.DatabaseName,
                Authentication = legacyMetadata.Authentication,
            };
        }
#pragma warning restore CS0618 // Type or member is obsolete

        // Return empty metadata if neither exists
        return new AzureMongoDBDataSourceMetadata();
    }
}
