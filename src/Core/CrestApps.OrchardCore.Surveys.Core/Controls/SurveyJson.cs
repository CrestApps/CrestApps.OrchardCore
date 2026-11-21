using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// The JSON options used for question settings and control data: property names as declared, enums as strings.
/// </summary>
public static class SurveyJson
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
