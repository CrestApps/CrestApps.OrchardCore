using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

internal sealed class AzureOpenAIConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AzureOpenAIConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureOpenAIConnectionDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        if (!string.Equals(connection.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<AzureOpenAIConnectionViewModel>("AzureOpenAIConnection_Edit", model =>
        {
            var metadata = connection.As<AzureOpenAIConnectionMetadata>();

            model.AuthenticationTypes =
            [
                new (S["Default authentication"], nameof(AzureAuthenticationType.Default)),
                new (S["Managed identity"], nameof(AzureAuthenticationType.ManagedIdentity)),
                new (S["API Key"], nameof(AzureAuthenticationType.ApiKey)),
            ];

            model.AuthenticationType = metadata.AuthenticationType;
            model.HasApiKey = !string.IsNullOrEmpty(metadata.ApiKey);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        if (!string.Equals(connection.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new AzureOpenAIConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = connection.As<AzureOpenAIConnectionMetadata>();

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);

        if (model.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(metadata.ApiKey) && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required field."]);
        }

        if (hasNewKey)
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            metadata.ApiKey = protector.Protect(model.ApiKey);
        }

        metadata.AuthenticationType = model.AuthenticationType;

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
