using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the hidden and calculated variables of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionVariablesDisplayDriver : DisplayDriver<SurveyDefinition>
{
    internal readonly IStringLocalizer S;

    public SurveyDefinitionVariablesDisplayDriver(IStringLocalizer<SurveyDefinitionVariablesDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Initialize<SurveyVariablesEditViewModel>("SurveyDefinitionVariables_Edit", model => model.Variables = definition.Variables.ToList())
            .Location("Content:1#Variables;4");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyVariablesEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var variables = new List<SurveyVariable>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in model.Variables.Where(variable => variable is not null && !string.IsNullOrWhiteSpace(variable.Name)))
        {
            variable.Name = variable.Name.Trim();

            if (!SurveyNames.IsValidVariableCode(variable.Name) || !names.Add(variable.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variables), S["The variable {0} needs a unique code of letters, digits and underscores.", variable.Name]);

                continue;
            }

            variable.Formula = variable.Kind == VariableKind.Calculated ? variable.Formula?.Trim() : null;
            variable.AcceptFromUrl = variable.Kind == VariableKind.Hidden && variable.AcceptFromUrl;
            variables.Add(variable);
        }

        definition.Variables = variables;

        return Edit(definition, context);
    }
}
