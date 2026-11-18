using System.Security.Claims;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Services;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// Describes staff members to Skills: their names, and that a person may change a staff member's skills and certificates
/// only when their staff scope covers them, as the staff editor's Skills card and "add certificate" require.
/// </summary>
internal sealed class StaffSkillSubjectProvider : ISkillSubjectProvider
{
    private readonly IStaffDirectory _directory;
    private readonly IStaffScopeService _scopeService;

    public StaffSkillSubjectProvider(IStaffDirectory directory, IStaffScopeService scopeService)
    {
        _directory = directory;
        _scopeService = scopeService;
    }

    public string Kind => SkillSubjectKind.Staff;

    public Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => _directory.GetNamesAsync(ids, cancellationToken);

    public async Task<bool> CanManageAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var found = await _directory.GetAsync([id], cancellationToken);

        return found.TryGetValue(id, out var staff) && await _scopeService.CanManageAsync(user, staff, cancellationToken);
    }
}
