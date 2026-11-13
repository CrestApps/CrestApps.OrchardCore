using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// Reads teams for Staff's own screens, recipes and reports. While the Teams module is off it reads none, so Staff
/// never touches team data it doesn't own.
/// </summary>
public sealed class StaffTeamReader
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StaffOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffTeamReader"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to reach the team manager only when teams are in use.</param>
    /// <param name="options">The Staff options.</param>
    public StaffTeamReader(IServiceProvider serviceProvider, IOptions<StaffOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <summary>
    /// Gets a value indicating whether team memberships are in use (the Teams module is on).
    /// </summary>
    public bool UsesTeams => _options.UsesTeams;

    /// <summary>
    /// Lists every team for a tree, or none while teams aren't in use.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams and their depth.</returns>
    public async Task<IReadOnlyList<(Team Team, int Depth)>> ListTreeAsync(CancellationToken cancellationToken = default)
        => UsesTeams ? await _serviceProvider.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken) : [];

    /// <summary>
    /// Gets every team, or none while teams aren't in use.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams.</returns>
    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
        => UsesTeams ? [.. await _serviceProvider.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken)] : [];
}
