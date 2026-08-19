using CrestApps.OrchardCore.Wizard.Contents;
using CrestApps.OrchardCore.Wizard.Core.Models;
using CrestApps.OrchardCore.Wizard.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Wizard.Drivers;

/// <summary>
/// Edits the settings of a <see cref="WizardPart"/> attachment: the content types a step may be, the
/// authoring display type, and whether the wizard requires an authenticated visitor.
/// </summary>
public sealed class WizardPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<WizardPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The manager used to list content type definitions.</param>
    /// <param name="stringLocalizer">The localizer used for validation messages.</param>
    public WizardPartSettingsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<WizardPartSettingsDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<WizardPartSettingsViewModel>("WizardPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<WizardPartSettings>();

            model.WizardPartSettings = settings;
            model.ContainedContentTypes = settings.ContainedContentTypes;
            model.DisplayType = settings.DisplayType;
            model.ContentTypes = [];
            model.Source = settings.ContainedStereotypes != null && settings.ContainedStereotypes.Length > 0 ? WizardPartSettingType.Stereotypes : WizardPartSettingType.ContentTypes;
            model.Stereotypes = string.Join(',', settings.ContainedStereotypes ?? []);
            model.CollapseContainedItems = settings.CollapseContainedItems;
            model.RequiresAuthenticatedUser = settings.RequiresAuthenticatedUser;
            model.CompletionPolicy = settings.CompletionPolicy;

            foreach (var contentTypeDefinition in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                model.ContentTypes.Add(contentTypeDefinition.Name, contentTypeDefinition.DisplayName);
            }
        }).Location("Content");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new WizardPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.ContainedContentTypes,
            m => m.DisplayType,
            m => m.Source,
            m => m.Stereotypes,
            m => m.CollapseContainedItems,
            m => m.RequiresAuthenticatedUser,
            m => m.CompletionPolicy);

        switch (model.Source)
        {
            case WizardPartSettingType.ContentTypes:
                SetContentTypes(context, model);
                break;
            case WizardPartSettingType.Stereotypes:
                SetStereotypes(context, model);
                break;
            default:
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Source), S["Content type source must be set with a valid value."]);
                break;
        }

        return Edit(contentTypePartDefinition, context);
    }

    private void SetStereotypes(UpdateTypePartEditorContext context, WizardPartSettingsViewModel model)
    {
        if (string.IsNullOrEmpty(model.Stereotypes))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Stereotypes), S["Please provide a stereotype."]);

            return;
        }

        context.Builder.WithSettings(new WizardPartSettings
        {
            ContainedContentTypes = [],
            ContainedStereotypes = model.Stereotypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            DisplayType = model.DisplayType,
            CollapseContainedItems = model.CollapseContainedItems,
            RequiresAuthenticatedUser = model.RequiresAuthenticatedUser,
            CompletionPolicy = model.CompletionPolicy,
        });
    }

    private void SetContentTypes(UpdateTypePartEditorContext context, WizardPartSettingsViewModel model)
    {
        if (model.ContainedContentTypes == null || model.ContainedContentTypes.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContainedContentTypes), S["At least one content type must be selected."]);

            return;
        }

        context.Builder.WithSettings(new WizardPartSettings
        {
            ContainedContentTypes = model.ContainedContentTypes,
            ContainedStereotypes = [],
            DisplayType = model.DisplayType,
            CollapseContainedItems = model.CollapseContainedItems,
            RequiresAuthenticatedUser = model.RequiresAuthenticatedUser,
            CompletionPolicy = model.CompletionPolicy,
        });
    }
}
