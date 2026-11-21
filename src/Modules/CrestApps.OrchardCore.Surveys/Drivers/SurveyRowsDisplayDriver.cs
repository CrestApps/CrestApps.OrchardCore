using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the rows of question types that use rows: matrix rows, matching prompts and items.
/// </summary>
internal sealed class SurveyRowsDisplayDriver : DisplayDriver<SurveyQuestion>
{
    private readonly ISurveyControlRegistry _registry;

    public SurveyRowsDisplayDriver(ISurveyControlRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => _registry.GetControl(model?.ControlType)?.Has(SurveyControlCapabilities.UsesRows) == true;

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion question, BuildEditorContext context)
        => Initialize<SurveyRowsEditViewModel>("SurveyRows_Edit", model => model.Rows = question.Rows.ToList()).Location("Content:3");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion question, UpdateEditorContext context)
    {
        var model = new SurveyRowsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rows = new List<SurveySubQuestion>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in model.Rows.Where(row => row is not null && !string.IsNullOrWhiteSpace(row.Text)))
        {
            if (string.IsNullOrWhiteSpace(row.Id) || !ids.Add(row.Id))
            {
                row.Id = IdGenerator.GenerateId();
                ids.Add(row.Id);
            }

            var name = SurveyNames.IsValidVariableCode(row.Name?.Trim()) ? row.Name.Trim() : $"r{rows.Count + 1}";

            for (var i = 2; !names.Add(name); i++)
            {
                name = $"r{rows.Count + 1}_{i}";
            }

            row.Name = name;
            row.Text = row.Text.Trim();
            rows.Add(row);
        }

        question.Rows = rows;

        return Edit(question, context);
    }
}
