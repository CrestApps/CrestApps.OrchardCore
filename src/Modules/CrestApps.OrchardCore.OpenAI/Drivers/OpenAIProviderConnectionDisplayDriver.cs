using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

internal sealed class OpenAIProviderConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public OpenAIProviderConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<OpenAIProviderConnectionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        if (!string.Equals(connection.ProviderName, OpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<OpenAIConnectionViewModel>("OpenAIConnection_Edit", model =>
        {
            var metadata = connection.As<OpenAIProviderConnectionMetadata>();

            model.Endpoint = metadata.Endpoint?.ToString();
            model.HasApiKey = !string.IsNullOrEmpty(metadata.ApiKey);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        if (!string.Equals(connection.ProviderName, OpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new OpenAIConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = connection.As<OpenAIProviderConnectionMetadata>();

        if (string.IsNullOrEmpty(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint is a required field"]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint must be a valid address."]);
        }
        else
        {
            metadata.Endpoint = endpoint;
        }

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);

        if (!string.IsNullOrEmpty(metadata.ApiKey) && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["API key is required field."]);
        }

        if (hasNewKey)
        {
            var protector = _dataProtectionProvider.CreateProtector(OpenAIConstants.ConnectionProtectorName);

            metadata.ApiKey = protector.Protect(model.ApiKey);
        }

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
