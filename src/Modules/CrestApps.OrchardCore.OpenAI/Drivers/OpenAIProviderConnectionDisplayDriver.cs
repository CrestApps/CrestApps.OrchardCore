using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

internal sealed class OpenAIProviderConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProviderConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenAIProviderConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<OpenAIProviderConnectionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
        _endpointPolicy = endpointPolicy;
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        if (!string.Equals(connection.ClientName, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<OpenAIConnectionViewModel>("OpenAIConnection_Edit", model =>
        {
            var metadata = connection.GetOrCreate<OpenAIConnectionMetadata>();

            model.Endpoint = metadata.Endpoint?.ToString();
            model.HasApiKey = ProtectedSettingExtensions.HasSecret(metadata.ApiKey);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        if (!string.Equals(connection.ClientName, OpenAIConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new OpenAIConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = connection.GetOrCreate<OpenAIConnectionMetadata>();

        if (string.IsNullOrEmpty(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint is a required field"]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint must be a valid address."]);
        }
        else if (!_endpointPolicy.IsAllowed(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }
        else
        {
            metadata.Endpoint = endpoint;
        }

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);

        if (string.IsNullOrEmpty(metadata.ApiKey) && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required field."]);
        }

        metadata.ApiKey = _dataProtectionProvider.ApplySecret(AIConstants.ConnectionProtectorName, metadata.ApiKey, model.ApiKey);

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
