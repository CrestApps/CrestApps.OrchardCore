using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// A persona in the admin list, with whether it is saved or comes from a file.
/// </summary>
public class AIPersonaSummaryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaSummaryViewModel"/> class.
    /// </summary>
    /// <param name="persona">The persona.</param>
    /// <param name="isFromFile">Whether the persona comes from a file rather than being saved.</param>
    public AIPersonaSummaryViewModel(AIPersona persona, bool isFromFile)
    {
        Persona = persona;
        IsFromFile = isFromFile;
    }

    /// <summary>
    /// Gets the persona.
    /// </summary>
    public AIPersona Persona { get; }

    /// <summary>
    /// Gets a value indicating whether the persona comes from a file. A file persona cannot be deleted, and saving
    /// it creates a saved copy that replaces the file.
    /// </summary>
    public bool IsFromFile { get; }
}
