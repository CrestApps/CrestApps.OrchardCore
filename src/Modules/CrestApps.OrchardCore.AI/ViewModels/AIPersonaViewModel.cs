using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Backs the AI persona editor.
/// </summary>
public class AIPersonaViewModel
{
    /// <summary>
    /// Gets or sets the technical name profiles and interactions link to. It cannot change once saved.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown in editors.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description shown in editors.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the persona instructions, written as a Liquid template.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the name the assistant uses for itself.
    /// </summary>
    public string AssistantName { get; set; }

    /// <summary>
    /// Gets or sets the language the persona speaks, as a BCP-47 tag.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the deployment the voice is chosen from.
    /// </summary>
    public string VoiceDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific voice.
    /// </summary>
    public string VoiceName { get; set; }

    /// <summary>
    /// Gets or sets the optional avatar image URL.
    /// </summary>
    public string AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the technical name can still be chosen. Not posted back.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the persona comes from a file, so saving it creates a saved copy
    /// that replaces the file. Not posted back.
    /// </summary>
    [BindNever]
    public bool IsFromFile { get; set; }

    /// <summary>
    /// Gets or sets the deployments that can speak, grouped by kind. Not posted back.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> VoiceDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the realtime deployments among <see cref="VoiceDeployments"/>, whose voices come
    /// from a different list than text-to-speech voices. Not posted back.
    /// </summary>
    [BindNever]
    public string[] RealtimeDeploymentNames { get; set; } = [];
}
