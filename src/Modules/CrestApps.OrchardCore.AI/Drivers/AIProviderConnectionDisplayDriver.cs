using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProviderConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly INamedCatalog<AIProviderConnection> _connectionsCatalog;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    public AIProviderConnectionDisplayDriver(
        INamedCatalog<AIProviderConnection> connectionsCatalog,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<AIProviderConnectionDisplayDriver> stringLocalizer)
    {
        _connectionsCatalog = connectionsCatalog;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProviderConnection connection, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProviderConnection_Fields_SummaryAdmin", connection).Location("Content:1"),
            View("AIProviderConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"),
            View("AIProviderConnection_DefaultTags_SummaryAdmin", connection).Location("Tags:5"),
            View("AIProviderConnection_DefaultMeta_SummaryAdmin", connection).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<AIProviderConnectionFieldsViewModel>("AIProviderConnectionFields_Edit", model =>
        {
            model.DisplayText = connection.DisplayText;
            model.Name = connection.Name;
            model.DefaultDeploymentName = connection.DefaultDeploymentName;
            model.DefaultEmbeddingDeploymentName = connection.DefaultEmbeddingDeploymentName;
            model.DefaultIntentDeploymentName = connection.DefaultIntentDeploymentName;
            model.DefaultImagesDeploymentName = connection.DefaultImagesDeploymentName;
            model.IsDefault = connection.IsDefault;
            model.IsNew = context.IsNew;

        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        var model = new AIProviderConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            if (string.IsNullOrEmpty(model.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _connectionsCatalog.FindByNameAsync(model.Name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another connection with the same name exists."]);
            }

            connection.Name = model.Name;
        }

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Title is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.DefaultDeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultDeploymentName), S["Default deployment name is required."]);
        }

        connection.DisplayText = model.DisplayText;
        connection.DefaultDeploymentName = model.DefaultDeploymentName;
        connection.DefaultEmbeddingDeploymentName = model.DefaultEmbeddingDeploymentName;
        connection.DefaultIntentDeploymentName = model.DefaultIntentDeploymentName;
        connection.DefaultImagesDeploymentName = model.DefaultImagesDeploymentName;
        connection.IsDefault = model.IsDefault;

        _shellReleaseManager.RequestRelease();

        return Edit(connection, context);
    }
}
