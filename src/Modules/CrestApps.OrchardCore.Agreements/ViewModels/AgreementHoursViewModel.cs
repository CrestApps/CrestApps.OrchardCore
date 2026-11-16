using CrestApps.OrchardCore.Agreements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Agreements.ViewModels;

/// <summary>
/// The hours an agreement covers, on its editor, with how they stand now.
/// </summary>
public class AgreementHoursViewModel
{
    /// <summary>
    /// Gets or sets how it covers hours.
    /// </summary>
    public AgreementHoursKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the hours of the block, or of each month.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets what each entry is rounded up to, in minutes.
    /// </summary>
    public int RoundingMinutes { get; set; }

    /// <summary>
    /// Gets or sets how the hours stand in the current period, for a saved agreement that covers hours.
    /// </summary>
    [BindNever]
    public AgreementBalance Balance { get; set; }
}
