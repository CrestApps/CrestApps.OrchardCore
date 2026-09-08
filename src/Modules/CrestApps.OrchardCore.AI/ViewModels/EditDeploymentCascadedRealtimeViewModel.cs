using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The editor for a cascaded realtime deployment: the three deployments it chains together to answer
/// speech with speech.
/// </summary>
public sealed class EditDeploymentCascadedRealtimeViewModel
{
    /// <summary>
    /// Gets or sets the technical name of the deployment that transcribes the user's speech.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the chat deployment that generates the reply.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the deployment that speaks the reply.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployments that can transcribe the user's speech.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployments that can generate the reply.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployments that can speak the reply.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; } = [];
}
