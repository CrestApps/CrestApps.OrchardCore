using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Drivers;

internal sealed class AzureAISearchAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AzureAISearchAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureAISearchAIDataSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.AzureAISearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditAzureAISearchAIDataSourceViewModel>("AzureAISearchAIDataSource_Edit", model =>
        {
            var metadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();
            model.Endpoint = metadata.Endpoint;
            model.AuthenticationType = metadata.GetAuthenticationType();
            model.IndexName = metadata.IndexName;
            model.IdentityClientId = metadata.IdentityClientId;
            model.HasApiKey = !string.IsNullOrWhiteSpace(metadata.ApiKey);
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.AzureAISearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditAzureAISearchAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var authenticationType = string.Equals(
            model.AuthenticationType,
            AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType,
            StringComparison.OrdinalIgnoreCase)
            ? AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType
            : string.Equals(
                model.AuthenticationType,
                AzureAISearchSourceMetadata.DefaultAuthenticationType,
                StringComparison.OrdinalIgnoreCase)
                ? AzureAISearchSourceMetadata.DefaultAuthenticationType
                : AzureAISearchSourceMetadata.ApiKeyAuthenticationType;
        var existingMetadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Azure AI Search endpoint is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Azure AI Search index name is required."]);
        }

        if (string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(existingMetadata.ApiKey) &&
            string.IsNullOrWhiteSpace(model.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["Azure AI Search API key is required."]);
        }

        var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);

        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = model.Endpoint?.Trim(),
            AuthenticationType = authenticationType,
            IndexName = model.IndexName?.Trim(),
            IdentityClientId = string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? null
                : model.IdentityClientId?.Trim(),
            ApiKey = string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrWhiteSpace(model.ApiKey)
                    ? existingMetadata.ApiKey
                    : protector.Protect(model.ApiKey)
                : null,
        });

        return Edit(dataSource, context);
    }

    private static string GetSourceType(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.SourceType)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.SourceType;
    }
}
