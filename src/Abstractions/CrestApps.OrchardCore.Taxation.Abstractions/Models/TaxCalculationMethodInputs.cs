using System;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Describes the tax rule configuration fields a <see cref="Services.ITaxCalculationMethod"/> consumes.
/// The tax rule editor uses these flags to show only the fields that a selected calculation method needs,
/// which lets third-party methods contribute their own field requirements without changing the editor.
/// </summary>
[Flags]
public enum TaxCalculationMethodInputs
{
    /// <summary>
    /// The method requires no additional configuration fields.
    /// </summary>
    None = 0,

    /// <summary>
    /// The method uses the percentage rate field.
    /// </summary>
    Rate = 1,

    /// <summary>
    /// The method uses the fixed amount field.
    /// </summary>
    FixedAmount = 2,

    /// <summary>
    /// The method uses a tax table reference.
    /// </summary>
    TaxTable = 4,
}
