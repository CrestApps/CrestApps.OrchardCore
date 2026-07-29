using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelActivityBatchAIProfileViewModel
{
    /// <summary>
    /// Gets or sets the activity source.
    /// </summary>
    [BindNever]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier used by automated activities.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the selected subject flow uses the phone channel.
    /// </summary>
    [BindNever]
    public bool IsPhoneChannel { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment override.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment override.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice override.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets the available AI profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIProfiles { get; set; }

    /// <summary>
    /// Gets or sets the available speech-to-text deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; }

    /// <summary>
    /// Gets or sets the available text-to-speech deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; }

    /// <summary>
    /// Gets or sets the available text-to-speech voices.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechVoices { get; set; }
}
