using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIDeploymentDisplayDriver : DisplayDriver<OpenAIDeployment>
{
    private readonly OpenAIConnectionOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public OpenAIDeploymentDisplayDriver(
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IStringLocalizer<OpenAIDeploymentDisplayDriver> stringLocalizer)
    {
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OpenAIDeployment deployment, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OpenAIDeployment_Fields_SummaryAdmin", deployment).Location("Content:1"),
            View("OpenAIDeployment_Buttons_SummaryAdmin", deployment).Location("Actions:5"),
            View("OpenAIDeployment_DefaultTags_SummaryAdmin", deployment).Location("Tags:5"),
            View("OpenAIDeployment_DefaultMeta_SummaryAdmin", deployment).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(OpenAIDeployment deployment, BuildEditorContext context)
    {
        return Initialize<EditDeploymentViewModel>("OpenAIDeploymentFields_Edit", model =>
        {
            model.Name = deployment.Name;
            model.ConnectionName = deployment.ConnectionName;
            model.IsNew = context.IsNew;

            if (_connectionOptions.Connections.TryGetValue(deployment.Source, out var connections))
            {
                model.Connections = connections.Select(x => new SelectListItem(x.Name, x.Name)).ToArray();

                if (string.IsNullOrEmpty(model.ConnectionName) && connections.Count == 1)
                {
                    model.ConnectionName = connections.First().Name;
                }
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIDeployment deployment, UpdateEditorContext context)
    {
        if (!context.IsNew)
        {
            return Edit(deployment, context);
        }

        var model = new EditDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var name = model.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
        }

        if (!_connectionOptions.Connections.TryGetValue(deployment.Source, out var connections))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["There are no configured connection for the source: {0}.", deployment.Source]);
        }
        else
        {
            if (string.IsNullOrEmpty(model.ConnectionName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Connection name is required."]);
            }
            else
            {
                var connection = connections.FirstOrDefault(x => x.Name != null && x.Name.Equals(model.ConnectionName, StringComparison.OrdinalIgnoreCase));

                if (connection == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Invalid connection name provided."]);
                }
                else
                {
                    deployment.ConnectionName = connection.Name;
                }
            }
        }

        deployment.Name = name;

        return Edit(deployment, context);
    }
}
