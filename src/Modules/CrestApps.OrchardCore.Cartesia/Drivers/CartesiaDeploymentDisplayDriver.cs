using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Cartesia;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Cartesia.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Cartesia.Drivers;

/// <summary>
/// Edits the API key and endpoint a Cartesia deployment holds, its contained connection.
/// </summary>
internal sealed class CartesiaDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    private const string ApiKeyProperty = "ApiKey";
    private const string EndpointProperty = "Endpoint";

    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public CartesiaDeploymentDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<CartesiaDeploymentDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        if (!string.Equals(deployment.ClientName, CartesiaConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<CartesiaDeploymentViewModel>("CartesiaDeployment_Edit", model =>
        {
            model.Endpoint = GetProperty(deployment, EndpointProperty);
            model.HasApiKey = !string.IsNullOrEmpty(GetProperty(deployment, ApiKeyProperty));
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        if (!string.Equals(deployment.ClientName, CartesiaConstants.ClientName, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new CartesiaDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        deployment.Properties ??= new Dictionary<string, object>();

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            deployment.Properties.Remove(EndpointProperty);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint must be a valid address."]);
        }
        else
        {
            deployment.Properties[EndpointProperty] = model.Endpoint;
        }

        var hasNewKey = !string.IsNullOrWhiteSpace(model.ApiKey);

        if (!hasNewKey && string.IsNullOrEmpty(GetProperty(deployment, ApiKeyProperty)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["API key is required."]);
        }

        if (hasNewKey)
        {
            var protector = _dataProtectionProvider.CreateProtector(AIConstants.ConnectionProtectorName);

            deployment.Properties[ApiKeyProperty] = protector.Protect(model.ApiKey);
        }

        return Edit(deployment, context);
    }

    private static string GetProperty(AIDeployment deployment, string key)
        => deployment.Properties != null && deployment.Properties.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
}
