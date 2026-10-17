namespace CrestApps.OrchardCore.AI.Core.ViewModels;

/// <summary>
/// One persona a profile, profile template, chat interaction or workflow task can link to.
/// </summary>
public class AIPersonaOption
{
    /// <summary>
    /// Gets or sets the persona's technical name, which is what the link stores.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the text shown for the persona.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the persona's description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the persona's voice, so a voice picker can show which voice the persona brings.
    /// </summary>
    public string VoiceName { get; set; }
}
