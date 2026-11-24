using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The translations page of a survey: one culture's translation of every text of the draft.
/// </summary>
public class SurveyTranslationsViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the culture edited, or empty before one is chosen.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the cultures of the site, without its default culture.
    /// </summary>
    public List<SelectListItem> Cultures { get; set; } = [];

    /// <summary>
    /// Gets or sets the texts of the draft with their translations.
    /// </summary>
    public List<SurveyTranslationEntryViewModel> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the stamp of the draft the editor loaded (the ticks of its last change), to catch a concurrent edit.
    /// </summary>
    public long? DraftStamp { get; set; }

    /// <summary>
    /// Gets or sets how many texts are translated from their current original text.
    /// </summary>
    public int Translated { get; set; }

    /// <summary>
    /// Gets or sets how many translations were made from an original text that has changed since.
    /// </summary>
    public int Outdated { get; set; }

    /// <summary>
    /// Gets or sets how many translations were saved before Surveys kept their original text, to be checked once.
    /// </summary>
    public int Unchecked { get; set; }
}
