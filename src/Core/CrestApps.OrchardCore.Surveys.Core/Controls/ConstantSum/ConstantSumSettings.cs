namespace CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;

/// <summary>
/// The settings of a constant sum question. Its items are the question's rows.
/// </summary>
public sealed class ConstantSumSettings
{
    /// <summary>
    /// Gets or sets the total the numbers must add up to.
    /// </summary>
    public decimal Total { get; set; } = 100;

    /// <summary>
    /// Gets or sets the unit shown after each box, such as %.
    /// </summary>
    public string Unit { get; set; }
}
