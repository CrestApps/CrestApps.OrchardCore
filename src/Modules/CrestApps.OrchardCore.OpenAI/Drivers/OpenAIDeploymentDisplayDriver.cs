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

    public override Task<IDisplayResult> DisplayAsync(OpenAIDeployment model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OpenAIDeployment_Fields_SummaryAdmin", model).Location("Content:1"),
            View("OpenAIDeployment_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("OpenAIDeployment_DefaultTags_SummaryAdmin", model).Location("Tags:5"),
            View("OpenAIDeployment_DefaultMeta_SummaryAdmin", model).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(OpenAIDeployment model, BuildEditorContext context)
    {
        return Initialize<EditDeploymentViewModel>("OpenAIDeploymentFields_Edit", m =>
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

    public override async Task<IDisplayResult> UpdateAsync(OpenAIDeployment model, UpdateEditorContext context)
    {
        if (!context.IsNew)
        {
            return Edit(model, context);
        }

        var viewModel = new EditDeploymentViewModel();

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
