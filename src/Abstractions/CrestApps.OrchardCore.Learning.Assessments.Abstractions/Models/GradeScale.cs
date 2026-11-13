using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// Maps percentage bands to letters, or to pass and fail.
/// </summary>
public sealed class GradeScale : CatalogItem
{
    /// <summary>
    /// Gets or sets the unique name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the bands; each starts at its minimum percent and runs to the next band.
    /// </summary>
    public IList<GradeBand> Bands { get; set; } = [];

    /// <summary>
    /// Finds the band of a percentage.
    /// </summary>
    /// <param name="percent">The percentage.</param>
    /// <returns>The band, or <see langword="null"/>.</returns>
    public GradeBand Find(decimal percent)
        => Bands.Where(band => percent >= band.MinPercent).OrderByDescending(band => band.MinPercent).FirstOrDefault();
}

/// <summary>
/// A band of a grade scale.
/// </summary>
public sealed class GradeBand
{
    /// <summary>
    /// Gets or sets the lowest percentage of the band.
    /// </summary>
    public decimal MinPercent { get; set; }

    /// <summary>
    /// Gets or sets the label, such as A or Pass.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets whether the band passes.
    /// </summary>
    public bool Passing { get; set; }
}
