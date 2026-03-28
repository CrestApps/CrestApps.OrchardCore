using CrestApps.AI.Models;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

internal sealed class AzureSpeechDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AzureSpeechDeploymentDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureSpeechDeploymentDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        if (!string.Equals(deployment.ClientName, AzureOpenAIConstants.AzureSpeechProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<AzureSpeechDeploymentViewModel>("AzureSpeechDeployment_Edit", model =>
        {
            model.Endpoint = GetPropertyString(deployment, "Endpoint");

            var authTypeStr = GetPropertyString(deployment, "AuthenticationType");
            if (!string.IsNullOrEmpty(authTypeStr) && Enum.TryParse<AzureAuthenticationType>(authTypeStr, true, out var authType))
            {
                model.AuthenticationType = authType;
            }

            model.IdentityId = GetPropertyString(deployment, "IdentityId");
            model.HasApiKey = !string.IsNullOrEmpty(GetPropertyString(deployment, "ApiKey"));

            model.AuthenticationTypes =
            [
                new (S["Default authentication"], nameof(AzureAuthenticationType.Default)),
                new (S["Managed identity"], nameof(AzureAuthenticationType.ManagedIdentity)),
                new (S["API Key"], nameof(AzureAuthenticationType.ApiKey)),
            ];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        if (!string.Equals(deployment.ClientName, AzureOpenAIConstants.AzureSpeechProviderName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new AzureSpeechDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        deployment.Properties ??= new Dictionary<string, object>();

        if (model.Endpoint is null || !Uri.TryCreate(model.Endpoint, UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["A valid endpoint URL is required."]);
        }
        else
        {
            deployment.Properties["Endpoint"] = model.Endpoint;
        }

        deployment.Properties["AuthenticationType"] = model.AuthenticationType.ToString();

        var trimmedIdentityId = model.IdentityId?.Trim();
        deployment.Properties["IdentityId"] = string.IsNullOrEmpty(trimmedIdentityId) ? null : trimmedIdentityId;

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);
        var existingKey = GetPropertyString(deployment, "ApiKey");

        if (model.AuthenticationType == AzureAuthenticationType.ApiKey && string.IsNullOrEmpty(existingKey) && !hasNewKey)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required."]);
        }

        if (hasNewKey)
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            deployment.Properties["ApiKey"] = protector.Protect(model.ApiKey);
        }

        return Edit(deployment, context);
    }

    private static string GetPropertyString(AIDeployment deployment, string key)
    {
        if (deployment.Properties != null && deployment.Properties.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
