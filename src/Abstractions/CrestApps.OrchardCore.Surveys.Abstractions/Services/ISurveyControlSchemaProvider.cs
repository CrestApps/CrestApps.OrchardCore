using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Describes the settings of a question type as JSON schema, for the survey recipe schema that authors and AI tools
/// read. Register an implementation for a control whose settings need more than the default description, which reads
/// the public properties of its settings type.
/// </summary>
public interface ISurveyControlSchemaProvider
{
    /// <summary>
    /// Gets the JSON schema of a question type's settings, or <see langword="null"/> when the provider does not describe
    /// it.
    /// </summary>
    /// <param name="descriptor">The question type.</param>
    /// <returns>The schema as a JSON object, or <see langword="null"/>.</returns>
    JsonObject GetSettingsSchema(SurveyControlDescriptor descriptor);
}
