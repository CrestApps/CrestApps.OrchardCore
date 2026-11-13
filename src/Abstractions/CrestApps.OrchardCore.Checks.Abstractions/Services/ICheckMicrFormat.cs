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
    /// <exception cref="ArgumentException">A number the line can't hold, such as a routing number of the wrong length
    /// or an account number too long for its field.</exception>
    MicrLine Format(MicrLineRequest request);
}
