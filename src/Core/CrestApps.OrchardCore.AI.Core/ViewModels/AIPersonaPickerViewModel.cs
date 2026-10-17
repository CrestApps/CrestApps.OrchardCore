using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Core.ViewModels;

/// <summary>
/// Backs the persona picker shown on AI profiles, profile templates, chat interactions and workflow tasks.
/// </summary>
public class AIPersonaPickerViewModel
{
    /// <summary>
    /// Gets or sets the technical name of the linked persona, or <see langword="null"/> for none.
    /// </summary>
    public string PersonaId { get; set; }

    /// <summary>
    /// Gets or sets the personas to pick from. Not posted back.
    /// </summary>
    [BindNever]
    public IList<AIPersonaOption> Personas { get; set; } = [];
}
