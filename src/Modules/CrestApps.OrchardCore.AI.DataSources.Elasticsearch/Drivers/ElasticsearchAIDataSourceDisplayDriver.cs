using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Drivers;

internal sealed class ElasticsearchAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ElasticsearchAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<ElasticsearchAIDataSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.Elasticsearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditElasticsearchAIDataSourceViewModel>("ElasticsearchAIDataSource_Edit", model =>
        {
            var metadata = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();
            model.EnvironmentType = metadata.GetEnvironmentType();
            model.Url = metadata.Url;
            model.CloudId = metadata.CloudId;
            model.AuthenticationType = metadata.GetAuthenticationType();
            model.IndexName = metadata.IndexName;
            model.Username = metadata.Username;
            model.ApiKeyId = metadata.ApiKeyId;
            model.CertificateFingerprint = metadata.CertificateFingerprint;
            model.HasPassword = !string.IsNullOrWhiteSpace(metadata.Password);
            model.HasApiKey = !string.IsNullOrWhiteSpace(metadata.ApiKey);
            model.HasBase64ApiKey = !string.IsNullOrWhiteSpace(metadata.Base64ApiKey);
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.Elasticsearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditElasticsearchAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var environmentType = NormalizeEnvironmentType(model.EnvironmentType);
        var authenticationType = NormalizeAuthenticationType(model.AuthenticationType);
        var existingMetadata = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.SelfManagedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.Url))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Url), S["Elasticsearch URL is required."]);
        }

        if (string.Equals(environmentType, ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.CloudId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CloudId), S["Elastic Cloud ID is required."]);
        }

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

        var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);

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
                ? string.IsNullOrWhiteSpace(model.Password)
                    ? existingMetadata.Password
                    : protector.Protect(model.Password)
                : null,
            ApiKey = string.Equals(authenticationType, ElasticsearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrWhiteSpace(model.ApiKey)
                    ? existingMetadata.ApiKey
                    : protector.Protect(model.ApiKey)
                : null,
            Base64ApiKey = string.Equals(authenticationType, ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrWhiteSpace(model.Base64ApiKey)
                    ? existingMetadata.Base64ApiKey
                    : protector.Protect(model.Base64ApiKey)
                : null,
            ApiKeyId = string.Equals(authenticationType, ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? model.ApiKeyId?.Trim()
                : null,
            CertificateFingerprint = model.CertificateFingerprint?.Trim(),
        });

        return Edit(dataSource, context);
    }

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

    private static string GetSourceType(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.SourceType)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.SourceType;
    }
}
