using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Finds pay schedules by name.
/// </summary>
public sealed class PayrollPayScheduleIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Finds pay codes by code, class and kind.
/// </summary>
public sealed class PayrollPayCodeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the class.
    /// </summary>
    public string Class { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
