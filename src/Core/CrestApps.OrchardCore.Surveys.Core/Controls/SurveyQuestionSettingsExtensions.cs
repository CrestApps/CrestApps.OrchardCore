using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Reads and writes the control-specific settings of a question.
/// </summary>
public static class SurveyQuestionSettingsExtensions
{
    /// <summary>
    /// Reads the settings of a question, or new settings when it has none.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="question">The question.</param>
    /// <returns>The settings.</returns>
    public static TSettings GetSettings<TSettings>(this SurveyQuestion question)
        where TSettings : class, new()
    {
        if (question?.Settings is not { Count: > 0 } settings)
        {
            return new TSettings();
        }

        try
        {
            return settings.Deserialize<TSettings>(SurveyJson.Options) ?? new TSettings();
        }
        catch (JsonException)
        {
            return new TSettings();
        }
    }

    /// <summary>
    /// Writes the settings of a question.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="question">The question.</param>
    /// <param name="settings">The settings.</param>
    public static void SetSettings<TSettings>(this SurveyQuestion question, TSettings settings)
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(question);

        question.Settings = settings is null
            ? []
            : JsonSerializer.SerializeToNode(settings, SurveyJson.Options) as JsonObject ?? [];
    }

    /// <summary>
    /// Writes settings as JSON without the properties marked <see cref="SurveyKeyAttribute"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="settings">The settings.</param>
    /// <returns>The JSON without key values.</returns>
    public static JsonObject WithoutKey<TSettings>(TSettings settings)
        where TSettings : class
    {
        if (settings is null)
        {
            return [];
        }

        var json = JsonSerializer.SerializeToNode(settings, SurveyJson.Options) as JsonObject ?? [];

        foreach (var property in typeof(TSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<SurveyKeyAttribute>() is not null)
            {
                json.Remove(property.Name);
            }
        }

        return json;
    }
}
