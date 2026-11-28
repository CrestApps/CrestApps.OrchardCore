using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Finds the registered question types and their handlers, scorers and analyzers by name.
/// </summary>
public interface ISurveyControlRegistry
{
    /// <summary>
    /// Gets every registered question type, ordered by category and position.
    /// </summary>
    /// <returns>The descriptors.</returns>
    IReadOnlyList<SurveyControlDescriptor> GetControls();

    /// <summary>
    /// Gets every registered preset.
    /// </summary>
    /// <returns>The presets.</returns>
    IReadOnlyList<SurveyControlPresetDescriptor> GetPresets();

    /// <summary>
    /// Gets a question type, or <see langword="null"/> when it is not registered (its module is off).
    /// </summary>
    /// <param name="controlType">The name.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    SurveyControlDescriptor GetControl(string controlType);

    /// <summary>
    /// Gets a preset, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The preset, or <see langword="null"/>.</returns>
    SurveyControlPresetDescriptor GetPreset(string name);

    /// <summary>
    /// Gets the handler of a question type, or <see langword="null"/>.
    /// </summary>
    /// <param name="controlType">The name.</param>
    /// <returns>The handler, or <see langword="null"/>.</returns>
    ISurveyControlHandler GetHandler(string controlType);

    /// <summary>
    /// Gets the scorer of a question type, or <see langword="null"/>.
    /// </summary>
    /// <param name="controlType">The name.</param>
    /// <returns>The scorer, or <see langword="null"/>.</returns>
    ISurveyControlScorer GetScorer(string controlType);

    /// <summary>
    /// Gets the analyzer of a question type, or <see langword="null"/> when the type has none or does not declare
    /// <see cref="CrestApps.OrchardCore.Surveys.Controls.SurveyControlCapabilities.Aggregatable"/>.
    /// </summary>
    /// <param name="controlType">The name.</param>
    /// <returns>The analyzer, or <see langword="null"/>.</returns>
    ISurveyControlAnalyzer GetAnalyzer(string controlType);
}
