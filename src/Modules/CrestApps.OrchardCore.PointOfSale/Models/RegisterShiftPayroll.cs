namespace CrestApps.OrchardCore.PointOfSale.Models;

/// <summary>
/// The pay runs and exports that took a shift's declared tips, stored on the shift.
/// </summary>
public sealed class RegisterShiftPayroll
{
    /// <summary>
    /// Gets or sets the ids of the pay runs or exports that took them.
    /// </summary>
    public List<string> ConsumerIds { get; set; } = [];
}
