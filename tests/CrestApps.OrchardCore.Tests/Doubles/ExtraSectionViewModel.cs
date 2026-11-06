namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// The view model of a section another module adds to an editor. It is a public, unsealed class, as the model of an
/// <c>Initialize&lt;TModel&gt;</c> shape must be.
/// </summary>
public class ExtraSectionViewModel
{
    /// <summary>
    /// Gets or sets what the section shows.
    /// </summary>
    public string Note { get; set; }
}
