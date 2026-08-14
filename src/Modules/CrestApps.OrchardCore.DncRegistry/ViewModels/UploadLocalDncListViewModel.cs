using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for the local DNC list upload file editor shape.
/// </summary>
public class UploadLocalDncListViewModel
{
    /// <summary>
    /// Gets or sets the display name for the list.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the CSV file containing phone numbers.
    /// </summary>
    public IFormFile File { get; set; }

    /// <summary>
    /// Gets or sets the available country options.
    /// </summary>
    public SelectListItem[] CountryOptions { get; set; } = [];
}
