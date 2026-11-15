using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// A kind of education credit, such as CEU, CPD hours or PDU.
/// </summary>
public sealed class EducationCreditType : CatalogItem
{
    /// <summary>
    /// Gets or sets the name, such as CPD hours.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unit shown after an amount, such as hours.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether people can record external learning of this type.
    /// </summary>
    public bool AllowExternalLearning { get; set; } = true;

    /// <summary>
    /// Gets or sets when the type was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
