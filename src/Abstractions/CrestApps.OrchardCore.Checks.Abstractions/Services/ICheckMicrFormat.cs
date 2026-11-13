using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Lays out the check line (MICR) of one country's checks. Register one with
/// <see cref="ChecksServiceCollectionExtensions.AddCheckMicrFormat{TFormat}"/>; the United States format is built in.
/// </summary>
public interface ICheckMicrFormat
{
    /// <summary>
    /// Gets the two-letter ISO country code of the format.
    /// </summary>
    string Country { get; }

    /// <summary>
    /// Lays out a check line.
    /// </summary>
    /// <param name="request">The numbers the line holds.</param>
    /// <returns>The characters and their positions.</returns>
    MicrLine Format(MicrLineRequest request);
}
