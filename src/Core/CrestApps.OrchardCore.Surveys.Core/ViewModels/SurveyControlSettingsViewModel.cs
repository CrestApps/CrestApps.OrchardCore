using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Core.ViewModels;

/// <summary>
/// The model of a question type's settings editor.
/// </summary>
/// <typeparam name="TSettings">The settings type.</typeparam>
public class SurveyControlSettingsViewModel<TSettings>
    where TSettings : class, new()
{
    /// <summary>
    /// Gets or sets the settings.
    /// </summary>
    public TSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the question edited.
    /// </summary>
    [BindNever]
    public SurveyQuestion Question { get; set; }
}
