using CrestApps.OrchardCore.OpenAI.Functions;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using static CrestApps.OrchardCore.OpenAI.Models.AIChatProfile;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIChatProfileStore _profileStore;
    private readonly IModelDeploymentStore _modelDeploymentStore;
    private readonly IEnumerable<IOpenAIChatFunction> _functions;

    internal readonly IStringLocalizer S;

    public AIChatProfileDisplayDriver(
        IAIChatProfileStore profileStore,
        IModelDeploymentStore modelDeploymentStore,
        IEnumerable<IOpenAIChatFunction> functions,
        IStringLocalizer<AIChatProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _functions = functions;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIChatProfile model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIChatProfile_Fields_SummaryAdmin", model).Location("Content:1"),
            View("AIChatProfile_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("AIChatProfile_DefaultTags_SummaryAdmin", model).Location("Tags:5"),
            View("AIChatProfile_DefaultMeta_SummaryAdmin", model).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        return Initialize<EditAIChatProfileViewModel>("AIChatProfileName_Edit", async m =>
        {
            m.Name = model.Name;
            m.WelcomeMessage = model.WelcomeMessage;
            m.DeploymentId = model.DeploymentId;
            m.TitleType = model.TitleType;
            m.IsNew = context.IsNew;

            m.TitleTypes =
            [
                new SelectListItem(S["Set the first prompt as the title"], nameof(SessionTitleType.InitialPrompt)),
                new SelectListItem(S["Generate a title based on the first prompt"], nameof(SessionTitleType.Generated)),
            ];

            m.Functions = _functions.OrderBy(x => x.Name).Select(x => new FunctionEntry
            {
                Name = x.Name,
                Description = x.Description,
                IsSelected = model.FunctionNames?.Contains(x.Name) ?? false,
            }).ToArray();

            m.Deployments = [];

            var deployments = (await _modelDeploymentStore.GetAllAsync())
            .GroupBy(x => x.ConnectionName)
            .Select(x => new
            {
                GroupName = x.Key,
                Items = x.OrderBy(x => x.Name),
            });

            foreach (var deployment in deployments)
            {
                var group = new SelectListGroup
                {
                    Name = deployment.GroupName,
                };

                foreach (var item in deployment.Items)
                {
                    var option = new SelectListItem
                    {
                        Text = item.Name,
                        Value = item.Id,
                        Group = group,
                    };

                    m.Deployments.Add(option);
                }
            }

        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile model, UpdateEditorContext context)
    {
        var viewModel = new EditAIChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (context.IsNew)
        {
            // Set the name only during profile creation. Editing the name afterward is not allowed.
            var name = viewModel.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Name is required."]);
            }
            else if (await _profileStore.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A profile with this name already exists. The name must be unique."]);
            }

            model.Name = name;
        }

        if (string.IsNullOrEmpty(viewModel.DeploymentId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.DeploymentId), S["Deployment is required."]);
        }
        else if (await _modelDeploymentStore.FindByIdAsync(viewModel.DeploymentId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.DeploymentId), S["Invalid deployment provided."]);
        }

        var validFunctionNames = _functions.Select(x => x.Name).ToArray();

        model.FunctionNames = viewModel.Functions.Where(x => x.IsSelected && validFunctionNames.Contains(x.Name)).Select(x => x.Name).ToArray();

        model.DeploymentId = viewModel.DeploymentId;
        model.WelcomeMessage = viewModel.WelcomeMessage;
        model.TitleType = viewModel.TitleType;

        return Edit(model, context);
    }
}
