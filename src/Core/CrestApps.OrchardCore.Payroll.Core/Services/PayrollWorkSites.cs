using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Reads where a person works for payroll: the jurisdictions of their work site team (Payroll's card on the team,
/// with the Teams feature on), else the employer's own work jurisdictions (a business with one location).
/// </summary>
public static class PayrollWorkSites
{
    /// <summary>
    /// Gets the jurisdictions of a person's work site: the work site team's when it has any, else the employer's
    /// <see cref="Employer.WorkJurisdictions"/>, else none.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="profile">The pay profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The jurisdictions, such as CA-ON.</returns>
    public static Task<List<string>> JurisdictionsAsync(IServiceProvider services, EmployeePayProfile profile, CancellationToken cancellationToken = default)
        => JurisdictionsAsync(services, profile, null, cancellationToken);

    /// <summary>
    /// Gets the jurisdictions of a person's work site: the work site team's when it has any, else the employer's
    /// <see cref="Employer.WorkJurisdictions"/>, else none.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="profile">The pay profile.</param>
    /// <param name="employer">The profile's employer when the caller has it, or <see langword="null"/> to read it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The jurisdictions, such as CA-ON.</returns>
    public static async Task<List<string>> JurisdictionsAsync(IServiceProvider services, EmployeePayProfile profile, Employer employer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(profile);

        var team = await TeamJurisdictionsAsync(services, profile, cancellationToken);

        if (team.Count > 0)
        {
            return team;
        }

        if (employer is null && !string.IsNullOrEmpty(profile.EmployerId) && services.GetService<IEmployerManager>() is { } employers)
        {
            employer = await employers.FindByIdAsync(profile.EmployerId, cancellationToken);
        }

        return [.. (employer?.WorkJurisdictions ?? []).Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))];
    }

    private static async Task<List<string>> TeamJurisdictionsAsync(IServiceProvider services, EmployeePayProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(profile.WorkSiteTeamId) || services.GetService<PayrollTeamsFeature>() is null || services.GetService<ITeamManager>() is not { } teams
            || await teams.FindByIdAsync(profile.WorkSiteTeamId, cancellationToken) is not { } site
            || !site.TryGet<PayrollTeamSettings>(out var settings) || settings is null)
        {
            return [];
        }

        return [.. (settings.Jurisdictions ?? []).Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))];
    }
}
