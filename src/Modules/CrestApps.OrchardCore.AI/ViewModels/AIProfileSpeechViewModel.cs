using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The editor of a profile's voice and speech settings.
/// </summary>
public class AIProfileSpeechViewModel
{
    /// <summary>Gets or sets how fast the voice speaks, from 0.5 to 2.</summary>
    public double? SpeakingRate { get; set; }

    /// <summary>Gets or sets the emotion the voice speaks with, where the provider supports it.</summary>
    public string SpeakingEmotion { get; set; }

    /// <summary>Gets or sets the pronunciations, one <c>word = how to say it</c> per line.</summary>
    public string Pronunciations { get; set; }

    /// <summary>Gets or sets the voice used when the first one fails.</summary>
    public string FallbackTextToSpeechDeploymentName { get; set; }

    /// <summary>Gets or sets the words recognition listens for, one per line.</summary>
    public string BoostedKeywords { get; set; }

    /// <summary>Gets or sets the languages recognition listens for, separated by commas.</summary>
    public string RecognitionLanguages { get; set; }

    /// <summary>Gets or sets how readily the caller interrupts the AI, from 0 to 1.</summary>
    public double? InterruptionSensitivity { get; set; }

    /// <summary>Gets or sets a value indicating whether the AI says "mm-hm" while the caller talks.</summary>
    public bool EnableBackchanneling { get; set; }

    /// <summary>Gets or sets what it says, one per line.</summary>
    public string BackchannelPhrases { get; set; }

    /// <summary>Gets or sets the languages the AI may speak, separated by commas.</summary>
    public string AllowedLanguages { get; set; }

    /// <summary>Gets or sets the language it starts in.</summary>
    public string StartingLanguage { get; set; }

    /// <summary>Gets or sets the voices to fall back to.</summary>
    [BindNever]
    public IList<SelectListItem> SpeechDeployments { get; set; } = [];
}
