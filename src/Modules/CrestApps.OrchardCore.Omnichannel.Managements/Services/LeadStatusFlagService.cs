using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Keeps the flags only one lead status may carry, the default status and the converted status, on a single status.
/// Saving a status with one of them clears it from every other status.
/// </summary>
public sealed class LeadStatusFlagService
{
    private readonly INamedCatalogManager<LeadStatus> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusFlagService"/> class.
    /// </summary>
    /// <param name="manager">The lead status manager.</param>
    public LeadStatusFlagService(INamedCatalogManager<LeadStatus> manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Clears the default and converted flags from every status other than the one just saved, where it carries them.
    /// </summary>
    /// <param name="saved">The status that was just saved.</param>
    public async Task EnforceAsync(LeadStatus saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        if (!saved.IsDefault && !saved.IsConverted)
        {
            return;
        }

        foreach (var other in await _manager.GetAllAsync())
        {
            if (other.ItemId == saved.ItemId)
            {
                continue;
            }

            var changed = false;

            if (saved.IsDefault && other.IsDefault)
            {
                other.IsDefault = false;
                changed = true;
            }

            if (saved.IsConverted && other.IsConverted)
            {
                other.IsConverted = false;
                changed = true;
            }

            // Clearing a flag cannot break a status's own rules, but the handlers still decide what may be saved.
            if (changed && (await _manager.ValidateAsync(other)).Succeeded)
            {
                await _manager.UpdateAsync(other);
            }
        }
    }
}
