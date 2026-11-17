using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Reads where a person works for payroll: the jurisdictions of their work site team (Payroll's card on the team,
/// with the Teams feature on).
/// </summary>
public static class PayrollWorkSites
{
    /// <summary>
    /// Gets the jurisdictions of a person's work site, or none.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="profile">The pay profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The jurisdictions, such as CA-ON.</returns>
    public static async Task<List<string>> JurisdictionsAsync(IServiceProvider services, EmployeePayProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(profile.WorkSiteTeamId) || services.GetService<PayrollTeamsFeature>() is null || services.GetService<ITeamManager>() is not { } teams
            || await teams.FindByIdAsync(profile.WorkSiteTeamId, cancellationToken) is not { } site
            || !site.TryGet<PayrollTeamSettings>(out var settings) || settings is null)
        {
            return [];
        }

        return [.. settings.Jurisdictions.Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))];
    }
}
