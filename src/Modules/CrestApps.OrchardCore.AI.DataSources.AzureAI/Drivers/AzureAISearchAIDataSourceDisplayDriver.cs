using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Drivers;

internal sealed class AzureAISearchAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly AIDataSourceConnectionPicker _connectionPicker;
    private readonly IAIEndpointPolicy _endpointPolicy;
    private readonly AzureAISearchHostIdentity _hostIdentity;
    private readonly IDataConnectionResolver _dataConnectionResolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="connectionPicker">The data connection picker.</param>
    /// <param name="endpointPolicy">The policy for the endpoints AI features call.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="hostIdentity">Where the data source may sign in as the host; <see langword="null"/> refuses it everywhere.</param>
    /// <param name="dataConnectionResolver">Resolves the picked data connection; <see langword="null"/> while the Data Connectors feature is off.</param>
    public AzureAISearchAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        AIDataSourceConnectionPicker connectionPicker,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<AzureAISearchAIDataSourceDisplayDriver> stringLocalizer,
        AzureAISearchHostIdentity hostIdentity = null,
        IDataConnectionResolver dataConnectionResolver = null)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _connectionPicker = connectionPicker;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
        _hostIdentity = hostIdentity;
        _dataConnectionResolver = dataConnectionResolver;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
            AIDataSourceSourceTypes.AzureAISearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditAzureAISearchAIDataSourceViewModel>("AzureAISearchAIDataSource_Edit", async model =>
        {
            var metadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();
            model.DataConnectionId = AIDataSourceConnectionPicker.GetConnectionId(dataSource);
            model.CanPickDataConnection = await _connectionPicker.CanPickAsync();

            if (model.CanPickDataConnection)
            {
                model.DataConnections = await _connectionPicker.GetOptionsAsync(AzureAISearchDataConnectionClient.ConnectorNames, model.DataConnectionId);
            }

            model.Endpoint = metadata.Endpoint;
            model.AuthenticationType = metadata.GetAuthenticationType();
            model.IndexName = metadata.IndexName;
            model.IdentityClientId = metadata.IdentityClientId;
            model.HasApiKey = ProtectedSettingExtensions.HasSecret(metadata.ApiKey);
        }).Location(l => l.Zone("Content", "11").Card(S["Connection"], "20"));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
            AIDataSourceSourceTypes.AzureAISearch,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditAzureAISearchAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A data source that uses a data connection reads the endpoint and the key from the connection. Its own endpoint
        // and key stay stored, unused, so switching back to its own settings loses nothing; they are not exported while
        // the connection is used.
        var connectionId = await _connectionPicker.UpdateAsync(dataSource, model.DataConnectionId, AzureAISearchDataConnectionClient.ConnectorNames, context, Prefix, nameof(model.DataConnectionId));

        if (connectionId is not null)
        {
            if (string.IsNullOrWhiteSpace(model.IndexName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Azure AI Search index name is required."]);
            }

            if (await ConnectionSignsInAsHostAsync(connectionId, model.IndexName?.Trim()))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataConnectionId), S["Signing in as the site's host is only available on the default tenant, or for a search service and index the host approved for this tenant. Use an API key, or ask the host to approve this index."]);
            }

            var storedMetadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();
            storedMetadata.IndexName = model.IndexName?.Trim();
            dataSource.Put(storedMetadata);

            return Edit(dataSource, context);
        }

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
        var previousMetadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();

        // The stored key was given for one endpoint and one way of signing in; changing either drops it.
        var existingMetadata = SecretDestination.SameOrigin(previousMetadata.Endpoint, model.Endpoint) &&
            string.Equals(previousMetadata.GetAuthenticationType(), authenticationType, StringComparison.OrdinalIgnoreCase)
            ? previousMetadata
            : new AzureAISearchSourceMetadata();

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Azure AI Search endpoint is required."]);
        }
        else if (!_endpointPolicy.IsAllowed(model.Endpoint.Trim()))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
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

        // A managed identity or the default credential signs in as the host, which reads other tenants' indexes too
        // (review 10, B1).
        if (!string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(model.Endpoint) &&
            _hostIdentity?.IsApproved(model.Endpoint.Trim(), model.IndexName?.Trim(), model.IdentityClientId?.Trim()) != true)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AuthenticationType), S["Signing in as the site's host is only available on the default tenant, or for a search service and index the host approved for this tenant. Use an API key, or ask the host to approve this index."]);
        }

        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = model.Endpoint?.Trim(),
            AuthenticationType = authenticationType,
            IndexName = model.IndexName?.Trim(),
            IdentityClientId = string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? null
                : model.IdentityClientId?.Trim(),
            ApiKey = string.Equals(authenticationType, AzureAISearchSourceMetadata.ApiKeyAuthenticationType, StringComparison.OrdinalIgnoreCase)
                ? _dataProtectionProvider.ApplySecret(AIDataSourceProtectionConstants.SourceSecretPurpose, existingMetadata.ApiKey, model.ApiKey)
                : null,
        });

        return Edit(dataSource, context);
    }

    // A data connection without authentication signs in with the default Azure credential, which is the host's.
    private async Task<bool> ConnectionSignsInAsHostAsync(string connectionId, string indexName)
    {
        if (_dataConnectionResolver is null)
        {
            return false;
        }

        var resolution = await _dataConnectionResolver.ResolveForAsync(connectionId, AzureAISearchDataConnectionClient.ConnectorNames);

        return resolution.Succeeded &&
            resolution.Context.Connection.GetOrCreate<HttpConnectionSettings>() is { } settings &&
            AzureAISearchHostIdentity.SignsInAsHost(settings) &&
            _hostIdentity?.IsApproved(settings.BaseUrl, indexName, identityClientId: null) != true;
    }
}
