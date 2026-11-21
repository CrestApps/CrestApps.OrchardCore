using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.ViewModels;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Core.Drivers;

/// <summary>
/// The base of a question type's settings editor in the designer. It edits <c>SurveyQuestion.Settings</c> as
/// <typeparamref name="TSettings"/> with the shape <c>SurveyControlSettings_Edit__{ControlType}</c> (view
/// <c>SurveyControlSettings-{ControlType}.Edit.cshtml</c>), only for questions of its type.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public abstract class SurveyControlSettingsDisplayDriver<TSettings> : DisplayDriver<SurveyQuestion>
    where TSettings : class, new()
{
    /// <summary>
    /// Gets the name of the question type the driver edits.
    /// </summary>
    protected abstract string ControlType { get; }

    /// <summary>
    /// Gets the zone and position of the editor.
    /// </summary>
    protected virtual string Location => "Content:20";

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => model is not null && string.Equals(model.ControlType, ControlType, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion model, BuildEditorContext context)
        => Initialize<SurveyControlSettingsViewModel<TSettings>>($"{SurveyControlShapes.Settings}_Edit__{SurveyControlShapes.Encode(ControlType)}", viewModel =>
        {
            viewModel.Settings = model.GetSettings<TSettings>();
            viewModel.Question = model;
        }).Location(Location);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion model, UpdateEditorContext context)
    {
        var viewModel = new SurveyControlSettingsViewModel<TSettings>
        {
            Settings = model.GetSettings<TSettings>(),
        };

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var settings = viewModel.Settings ?? new TSettings();

        await UpdateSettingsAsync(model, settings, context);
        model.SetSettings(settings);

        return Edit(model, context);
    }

    /// <summary>
    /// Checks and adjusts the posted settings before they are saved.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="settings">The posted settings.</param>
    /// <param name="context">The editor context.</param>
    /// <returns>A task.</returns>
    protected virtual Task UpdateSettingsAsync(SurveyQuestion question, TSettings settings, UpdateEditorContext context)
        => Task.CompletedTask;
}
