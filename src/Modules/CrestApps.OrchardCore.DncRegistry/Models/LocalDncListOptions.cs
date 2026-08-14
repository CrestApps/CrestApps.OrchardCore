using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents the filter and display options for the local DNC list admin page.
/// </summary>
public class LocalDncListOptions
{
    /// <summary>
    /// Gets or sets the start index of items on the current page.
    /// </summary>
    [BindNever]
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the end index of items on the current page.
    /// </summary>
    [BindNever]
    public int EndIndex { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    [BindNever]
    public int TotalItemCount { get; set; }
}
