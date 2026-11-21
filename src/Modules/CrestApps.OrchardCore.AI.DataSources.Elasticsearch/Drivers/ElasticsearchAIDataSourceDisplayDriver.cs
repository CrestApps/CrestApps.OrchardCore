using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Drivers;

internal sealed class ElasticsearchAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ElasticsearchDataSourceOptions _options;
    private readonly AIDataSourceConnectionPicker _connectionPicker;
    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="options">The global Elasticsearch data source options.</param>
    /// <param name="connectionPicker">The data connection picker.</param>
    /// <param name="endpointPolicy">The policy for the endpoints AI features call.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ElasticsearchAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ElasticsearchDataSourceOptions> options,
        AIDataSourceConnectionPicker connectionPicker,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<ElasticsearchAIDataSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _options = options.Value;
        _connectionPicker = connectionPicker;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
            AIDataSourceSourceTypes.Elasticsearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditElasticsearchAIDataSourceViewModel>("ElasticsearchAIDataSource_Edit", async model =>
        {
            var metadata = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();
            model.DataConnectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource);
            model.CanPickDataConnection = await _connectionPicker.CanPickAsync();

            if (model.CanPickDataConnection)
            {
                model.DataConnections = await _connectionPicker.GetOptionsAsync(ElasticsearchDataConnectionSettings.ConnectorNames, model.DataConnectionId);
            }

            model.EnvironmentType = metadata.GetEnvironmentType();
            model.Url = metadata.Url;
            model.CloudId = metadata.CloudId;
            model.AuthenticationType = metadata.GetAuthenticationType();
            model.IndexName = metadata.IndexName;
            model.Username = metadata.Username;
            model.ApiKeyId = metadata.ApiKeyId;
            model.CertificateFingerprint = metadata.CertificateFingerprint;
            model.HasPassword = ProtectedSettingExtensions.HasSecret(metadata.Password);
            model.HasApiKey = ProtectedSettingExtensions.HasSecret(metadata.ApiKey);
            model.HasBase64ApiKey = ProtectedSettingExtensions.HasSecret(metadata.Base64ApiKey);
            model.HasDefaultConnection = _options.HasConnection;
            model.UseDefaultConnection = _options.HasConnection && !HasStoredConnection(metadata) && model.DataConnectionId is null;
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
            AIDataSourceSourceTypes.Elasticsearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditElasticsearchAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A data source that uses a data connection reads the URL and the credentials from the connection. Its own URL
        // and credentials stay stored, unused, so switching back to its own settings loses nothing; they are not
        // exported while the connection is used.
        var storedConnectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource);
        var connectionId = await _connectionPicker.UpdateAsync(dataSource, model.DataConnectionId, ElasticsearchDataConnectionSettings.ConnectorNames, context, Prefix, nameof(model.DataConnectionId));

        if (connectionId is not null)
        {
            if (string.IsNullOrWhiteSpace(model.IndexName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Elasticsearch index name is required."]);
            }

            var storedMetadata = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();
            storedMetadata.IndexName = model.IndexName?.Trim();
            dataSource.Put(storedMetadata);

            return Edit(dataSource, context);
        }

        // When a connection is configured globally, the operator can opt into it and only the index
        // name is stored on the data source.
        if (_options.HasConnection && model.UseDefaultConnection)
        {
            if (string.IsNullOrWhiteSpace(model.IndexName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Elasticsearch index name is required."]);
            }

            dataSource.Put(new ElasticsearchSourceMetadata
            {
                IndexName = model.IndexName?.Trim(),
            });

            return Edit(dataSource, context);
        }

        var environmentType = NormalizeEnvironmentType(model.EnvironmentType);
        var authenticationType = NormalizeAuthenticationType(model.AuthenticationType);
        var stored = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();
        var existingMetadata = KeptCredentials(stored, environmentType, model.Url, model.CloudId, authenticationType);

        ValidateUrl(context, environmentType, model.Url, stored, storedConnectionId);
        ValidateCloudId(context, environmentType, model.CloudId, stored, storedConnectionId);

        if (string.IsNullOrWhiteSpace(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Elasticsearch index name is required."]);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(model.Username))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Username), S["Username is required for basic authentication."]);
            }

            if (string.IsNullOrWhiteSpace(existingMetadata.Password) && string.IsNullOrWhiteSpace(model.Password))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["Password is required for basic authentication."]);
            }
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(existingMetadata.ApiKey) &&
            string.IsNullOrWhiteSpace(model.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required for API key authentication."]);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(existingMetadata.Base64ApiKey) &&
            string.IsNullOrWhiteSpace(model.Base64ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Base64ApiKey), S["A base64 API key is required for base64 API key authentication."]);
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(model.ApiKeyId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKeyId), S["API key ID is required for key ID and key authentication."]);
            }

            if (string.IsNullOrWhiteSpace(existingMetadata.ApiKey) && string.IsNullOrWhiteSpace(model.ApiKey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required for key ID and key authentication."]);
            }
        }

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(authenticationType, ElasticsearchSourceMetadata.NoneAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AuthenticationType), S["Elastic Cloud connections require an authentication type and matching credentials."]);
        }

        dataSource.Put(new ElasticsearchSourceMetadata
        {
            EnvironmentType = environmentType,
            Url = string.Equals(environmentType, ElasticsearchSourceMetadata.SelfManagedEnvironmentType, StringComparison.OrdinalIgnoreCase)
                ? model.Url?.Trim()
                : null,
            CloudId = string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase)
                ? model.CloudId?.Trim()
                : null,
            AuthenticationType = authenticationType,
            IndexName = model.IndexName?.Trim(),
            Username = string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? model.Username?.Trim()
                : null,
            Password = string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? _dataProtectionProvider.ApplySecret(AIDataSourceProtectionConstants.SourceSecretPurpose, existingMetadata.Password, model.Password)
                : null,
            ApiKey = string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? _dataProtectionProvider.ApplySecret(AIDataSourceProtectionConstants.SourceSecretPurpose, existingMetadata.ApiKey, model.ApiKey)
                : null,
            Base64ApiKey = string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? _dataProtectionProvider.ApplySecret(AIDataSourceProtectionConstants.SourceSecretPurpose, existingMetadata.Base64ApiKey, model.Base64ApiKey)
                : null,
            ApiKeyId = string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? model.ApiKeyId?.Trim()
                : null,
            CertificateFingerprint = model.CertificateFingerprint?.Trim(),
        });

        return Edit(dataSource, context);
    }

    // A self-managed cluster's URL is required, and a new one must not be on the site's own network. The stored URL is
    // not checked again (review 8, G7), so a cluster on a private network saved before the check can still be edited;
    // only a URL the stored source used as its own self-managed cluster is exempt (review 9, K6).
    private void ValidateUrl(UpdateEditorContext context, string environmentType, string url, ElasticsearchSourceMetadata stored, string storedConnectionId)
    {
        if (!string.Equals(environmentType, ElasticsearchSourceMetadata.SelfManagedEnvironmentType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(EditElasticsearchAIDataSourceViewModel.Url), S["Elasticsearch URL is required."]);
        }
        else if (!ElasticsearchAIDataSourceSourceHandler.UsedStoredUrl(stored, storedConnectionId, url) && !_endpointPolicy.IsAllowed(url.Trim()))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(EditElasticsearchAIDataSourceViewModel.Url), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }
    }

    // A Cloud ID is required, and the address it decodes to must not be on the site's own network, since the client
    // derives the cluster's address from the ID (review 10, A3). As for a URL, a Cloud ID the stored source used is not
    // checked again.
    private void ValidateCloudId(UpdateEditorContext context, string environmentType, string cloudId, ElasticsearchSourceMetadata stored, string storedConnectionId)
    {
        if (!string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cloudId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(EditElasticsearchAIDataSourceViewModel.CloudId), S["Elastic Cloud ID is required."]);
        }
        else if (!ElasticsearchAIDataSourceSourceHandler.UsedStoredCloudId(stored, storedConnectionId, cloudId) &&
            ElasticsearchAIDataSourceSourceHandler.GetCloudIdError(cloudId, _endpointPolicy.IsAllowed) is { } error)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(EditElasticsearchAIDataSourceViewModel.CloudId), error == ElasticsearchAIDataSourceSourceHandler.InvalidCloudIdError
                ? S["This is not an Elastic Cloud ID. Copy the Cloud ID from the deployment's page in Elastic Cloud."]
                : S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }
    }

    // The stored credentials were given for one cluster and one way of signing in; changing either drops them.
    private static ElasticsearchSourceMetadata KeptCredentials(ElasticsearchSourceMetadata stored, string environmentType, string url, string cloudId, string authenticationType)
        => SendsToSamePlace(stored, environmentType, url, cloudId, authenticationType) ? stored : new ElasticsearchSourceMetadata();

    private static bool SendsToSamePlace(ElasticsearchSourceMetadata stored, string environmentType, string url, string cloudId, string authenticationType)
    {
        if (!string.Equals(NormalizeEnvironmentType(stored.EnvironmentType), environmentType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeAuthenticationType(stored.AuthenticationType), authenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase)
            ? string.Equals(stored.CloudId?.Trim(), cloudId?.Trim(), StringComparison.Ordinal)
            : SecretDestination.SameOrigin(stored.Url, url);
    }

    private static bool HasStoredConnection(ElasticsearchSourceMetadata metadata)
        => !string.IsNullOrWhiteSpace(metadata.Url) || !string.IsNullOrWhiteSpace(metadata.CloudId);

    private static string NormalizeEnvironmentType(string environmentType)
    {
        return string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase)
            ? ElasticsearchSourceMetadata.CloudHostedEnvironmentType
            : ElasticsearchSourceMetadata.SelfManagedEnvironmentType;
    }

    private static string NormalizeAuthenticationType(string authenticationType)
    {
        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.BasicAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.BasicAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.ApiKeyAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType;
        }

        if (string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType;
        }

        return ElasticsearchSourceMetadata.NoneAuthenticationType;
    }
}
