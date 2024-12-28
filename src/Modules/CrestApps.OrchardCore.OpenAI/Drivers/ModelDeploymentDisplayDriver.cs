using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class ModelDeploymentDisplayDriver : DisplayDriver<ModelDeployment>
{
    private readonly IModelDeploymentStore _deploymentStore;
    private readonly OpenAIConnectionOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public ModelDeploymentDisplayDriver(
        IModelDeploymentStore deploymentStore,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IStringLocalizer<ModelDeploymentDisplayDriver> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ModelDeployment model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ModelDeployment_Fields_SummaryAdmin", model).Location("Content:1"),
            View("ModelDeployment_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("ModelDeployment_DefaultTags_SummaryAdmin", model).Location("Tags:5"),
            View("ModelDeployment_DefaultMeta_SummaryAdmin", model).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(ModelDeployment model, BuildEditorContext context)
    {
        return Initialize<EditModelDeploymentViewModel>("ModelDeploymentName_Edit", m =>
        {
            m.Name = model.Name;
            m.ConnectionName = model.ConnectionName;
            m.IsNew = context.IsNew;

            if (_connectionOptions.Connections.TryGetValue(model.Source, out var connections))
            {
                m.Connections = connections.Select(x => new SelectListItem(x.Name, x.Name)).ToArray();
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ModelDeployment model, UpdateEditorContext context)
    {
        if (!context.IsNew)
        {
            return Edit(model, context);
        }

        var viewModel = new EditModelDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var name = viewModel.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Name is required."]);
        }

        if (!_connectionOptions.Connections.TryGetValue(model.Source, out var connections))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ConnectionName), S["There are no configured connection for the source: {0}.", model.Source]);
        }
        else
        {
            if (string.IsNullOrEmpty(viewModel.ConnectionName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ConnectionName), S["Connection name is required."]);
            }
            else
            {
                var connection = connections.FirstOrDefault(x => x.Name != null && x.Name.Equals(viewModel.ConnectionName, StringComparison.OrdinalIgnoreCase));

                if (connection == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ConnectionName), S["Invalid connection name provided."]);
                }
                else
                {
                    model.ConnectionName = connection.Name;
                }
            }
        }

        model.Name = name;

        return Edit(model, context);
    }
}
