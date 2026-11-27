using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Azure;
using CrestApps.Core.Azure.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

internal sealed class AzureOpenAIConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="endpointPolicy">The policy for the endpoints AI features call.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AzureOpenAIConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<AzureOpenAIConnectionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        if (!string.Equals(connection.ClientName, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        return Combine(
            Initialize<AzureOpenAIConnectionViewModel>("AzureOpenAIConnection_Edit", model => Populate(connection, model))
                .Location(l => l.Zone("Content", "5").Card(S["Connection"], "20")),
            Initialize<AzureOpenAIConnectionViewModel>("AzureOpenAIAuthentication_Edit", model => Populate(connection, model))
                .Location(l => l.Zone("Content", "5.1").Card(S["Authentication"], "30")));
    }

    private void Populate(AIProviderConnection connection, AzureOpenAIConnectionViewModel model)
    {
        var metadata = connection.GetOrCreate<AzureConnectionMetadata>();

        model.Endpoint = metadata.Endpoint?.ToString();
        model.AuthenticationTypes =
        [
            new (S["Default authentication"], nameof(AzureAuthenticationType.Default)),
                new (S["Managed identity"], nameof(AzureAuthenticationType.ManagedIdentity)),
                new (S["API key"], nameof(AzureAuthenticationType.ApiKey)),
            ];

        model.AuthenticationType = metadata.AuthenticationType;
        model.HasApiKey = ProtectedSettingExtensions.HasSecret(metadata.ApiKey);
        model.IdentityId = metadata.IdentityId;
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        if (!string.Equals(connection.ClientName, AzureOpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new AzureOpenAIConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = connection.GetOrCreate<AzureConnectionMetadata>();

        // The stored key was given for one endpoint and one way of signing in; changing either drops it.
        var storedKey = SecretDestination.Keep(metadata.ApiKey, metadata.AuthenticationType == model.AuthenticationType);

        if (model.Endpoint is null || !Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uri))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint is required field."]);
        }
        else if (!_endpointPolicy.IsAllowed(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }
        else
        {
            storedKey = SecretDestination.Keep(storedKey, SecretDestination.SameOrigin(metadata.Endpoint, uri));
            metadata.Endpoint = uri;
        }

        metadata.AuthenticationType = model.AuthenticationType;

        var trimmedIdentityId = model.IdentityId?.Trim();
        metadata.IdentityId = string.IsNullOrEmpty(trimmedIdentityId) ? null : trimmedIdentityId;

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);

        if (model.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(storedKey) && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required field."]);
        }

        metadata.ApiKey = _dataProtectionProvider.ApplySecret(AIConstants.ConnectionProtectorName, storedKey, model.ApiKey);

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
