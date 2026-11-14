using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// The tax categories a pay code's treatment is set for: the ones every country has
/// (<see cref="PayrollTaxCategories"/>) and those a country pack registers.
/// </summary>
public sealed class PayrollTaxCategoryOptions
{
    /// <summary>
    /// Gets the categories, by stored name, in the order they were added.
    /// </summary>
    public List<PayrollTaxCategory> Categories { get; } = [];
}

/// <summary>
/// A tax category.
/// </summary>
/// <param name="Name">The stored name.</param>
/// <param name="DisplayName">What people see.</param>
public sealed record PayrollTaxCategory(string Name, LocalizationSource DisplayName);

/// <summary>
/// The boxes of tax forms a pay code's amounts can go to, registered by country packs.
/// </summary>
public sealed class PayrollFormBoxOptions
{
    /// <summary>
    /// Gets the boxes, in the order they were added.
    /// </summary>
    public List<PayrollFormBox> Boxes { get; } = [];
}

/// <summary>
/// A box of a tax form.
/// </summary>
/// <param name="Name">The stored name, such as a form and its box.</param>
/// <param name="DisplayName">What people see.</param>
public sealed record PayrollFormBox(string Name, LocalizationSource DisplayName);
