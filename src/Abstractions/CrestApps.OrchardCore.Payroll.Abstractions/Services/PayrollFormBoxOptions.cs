using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// The boxes of tax forms a pay code's amounts can go to, registered by country filings packs.
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
public sealed record PayrollFormBox(string Name, [property: JsonIgnore] LocalizationSource DisplayName);
