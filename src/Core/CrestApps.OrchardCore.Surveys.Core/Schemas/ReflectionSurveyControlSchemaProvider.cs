using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Schemas;

/// <summary>
/// Describes a question type's settings from the public properties of its settings type. It is the description used
/// when no <see cref="ISurveyControlSchemaProvider"/> describes the type.
/// </summary>
public sealed class ReflectionSurveyControlSchemaProvider : ISurveyControlSchemaProvider
{
    /// <inheritdoc />
    public JsonObject GetSettingsSchema(SurveyControlDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var schema = SurveyModelSchemaBuilder.BuildSettings(descriptor.SettingsType);

        schema["title"] = descriptor.Name;

        return schema;
    }
}
