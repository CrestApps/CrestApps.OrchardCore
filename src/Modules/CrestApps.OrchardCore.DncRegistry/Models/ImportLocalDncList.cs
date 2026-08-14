using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents the import model used by display drivers to collect upload options for a local DNC list.
/// </summary>
public sealed class ImportLocalDncList
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
}
