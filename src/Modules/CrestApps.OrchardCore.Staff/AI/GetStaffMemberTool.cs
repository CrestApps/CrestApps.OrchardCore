using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Returns one staff member's record, to the people the staff profile page shows it to: those with
/// <see cref="StaffPermissions.ViewStaff"/>, the people who manage the person, and the person themselves. The date of
/// birth is returned only with <see cref="StaffPermissions.ViewSensitiveStaffData"/>, and pay only with
/// <see cref="StaffPermissions.ManageStaffPay"/>, as the editor and the Pay card show them. The home base address and the
/// badge are never returned. The history lists the employment changes the profile shows.
/// </summary>
internal sealed class GetStaffMemberTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getStaffMember";

    private const int HistoryEntries = 20;

    // The employment changes the profile's history shows; another module's entries (such as pay) are left out.
    private static readonly HashSet<string> _historyFields = new(StringComparer.Ordinal)
    {
        StaffHistoryFields.Created,
        StaffHistoryFields.JobTitle,
        StaffHistoryFields.Status,
        StaffHistoryFields.EmploymentType,
        StaffHistoryFields.Manager,
        StaffHistoryFields.Teams,
        StaffHistoryFields.WorkLocation,
        StaffHistoryFields.TerminationDate,
    };

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "staffMemberId": {
          "type": "string",
          "description": "The staff member's id."
        },
        "employeeNumber": {
          "type": "string",
          "description": "The staff member's employee (badge) number, when the id is not known."
        },
        "name": {
          "type": "string",
          "description": "The staff member's name, or part of it, when neither the id nor the employee number is known. When several people match, they are listed with their ids."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one staff member's full record with their id: employment details, manager, team memberships with dates and roles, capabilities, linked user, employment history and, when you may see them, date of birth and pay. Find the person by id, employee number or name.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => await HasAsync(services, caller, StaffPermissions.ViewStaff)
            || await HasAsync(services, caller, StaffPermissions.ViewOwnStaffProfile)
            || await ManagesAnybodyAsync(services, caller, cancellationToken);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        // As the profile page: those who may see staff, the people who manage the person, and the person themselves.
        // Nobody else is found, named or listed.
        var canSeeEveryone = await HasAsync(services, caller, StaffPermissions.ViewStaff);
        var scopeService = services.GetRequiredService<IStaffScopeService>();
        var callerId = caller.FindFirstValue(ClaimTypes.NameIdentifier);

        async Task<bool> CanSeeAsync(StaffMember person)
            => canSeeEveryone
                || (!string.IsNullOrEmpty(person.UserId) && person.UserId == callerId)
                || await scopeService.CanManageAsync(caller, person, cancellationToken);

        var (staff, error) = await FindStaffAsync(arguments, services, CanSeeAsync, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var directory = services.GetRequiredService<IStaffDirectory>();
        var teamReader = services.GetRequiredService<StaffTeamReader>();
        var teams = teamReader.UsesTeams
            ? (await teamReader.GetAllAsync(cancellationToken)).Where(team => team.ItemId is not null).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var history = (await services.GetRequiredService<IStaffHistoryService>().ListAsync(staff.ItemId, 100, cancellationToken))
            .Where(entry => _historyFields.Contains(entry.Field))
            .Take(HistoryEntries)
            .ToList();
        var managerIds = history.Where(entry => entry.Field == StaffHistoryFields.Manager).SelectMany(entry => new[] { entry.From, entry.To }).Append(staff.ManagerId);
        var names = await directory.GetNamesAsync(managerIds, cancellationToken);
        var directReports = await services.GetRequiredService<IStaffHierarchy>().GetReportsAsync(staff.ItemId, includeIndirect: false, cancellationToken);
        var historyResults = new List<object>();

        foreach (var entry in history)
        {
            var isManager = entry.Field == StaffHistoryFields.Manager;

            historyResults.Add(new
            {
                field = entry.Field,
                from = isManager && entry.From is not null ? names.GetValueOrDefault(entry.From, entry.From) : entry.From,
                to = isManager && entry.To is not null ? names.GetValueOrDefault(entry.To, entry.To) : entry.To,
                changedAt = await LocalAsync(services, entry.ChangedUtc),
            });
        }

        var canSeeSensitive = await HasAsync(services, caller, StaffPermissions.ViewSensitiveStaffData);
        object pay = null;

        if (await HasAsync(services, caller, StaffPermissions.ManageStaffPay) && staff.TryGet<StaffPayRates>(out var rates) && rates is not null)
        {
            pay = new
            {
                rates = rates.Rates.OrderBy(rate => rate.EffectiveFrom).Select(rate => new
                {
                    effectiveFrom = Date(rate.EffectiveFrom),
                    hourlyRate = rate.HourlyRate,
                    activityCodeId = rate.ActivityCodeId,
                }),
                annualSalary = rates.AnnualSalary,
                hourlyCost = rates.HourlyCost,
            };
        }

        return Json(new
        {
            id = staff.ItemId,
            displayName = staff.DisplayName,
            userId = staff.UserId,
            email = staff.Email,
            phone = staff.Phone,
            employeeNumber = staff.EmployeeNumber,
            externalId = staff.ExternalId,
            jobTitle = staff.JobTitle,
            employmentType = staff.EmploymentType.ToString(),
            status = staff.Status.ToString(),
            hireDate = Date(staff.HireDate),
            seniorityDate = Date(staff.SeniorityDate),
            terminationDate = Date(staff.TerminationDate),
            dateOfBirth = canSeeSensitive ? Date(staff.DateOfBirth) : null,
            manager = staff.ManagerId is null ? null : new { id = staff.ManagerId, name = names.GetValueOrDefault(staff.ManagerId) },
            directReports = directReports.Select(report => new { id = report.ItemId, name = report.DisplayName, jobTitle = report.JobTitle }),
            mainTeam = staff.PrimaryTeamId is null ? null : new { id = staff.PrimaryTeamId, name = teams.GetValueOrDefault(staff.PrimaryTeamId) },
            teamMemberships = staff.TeamMemberships.Select(membership => new
            {
                teamId = membership.TeamId,
                teamName = teams.GetValueOrDefault(membership.TeamId ?? string.Empty),
                role = membership.Role.ToString(),
                effectiveFrom = Date(membership.EffectiveFrom),
                effectiveTo = Date(membership.EffectiveTo),
            }),
            timeZoneId = staff.TimeZoneId,
            workLocation = staff.WorkLocation,
            hasHomeBase = staff.HomeBase is not null,
            contractedHoursPerWeek = staff.ContractedHoursPerWeek,
            capabilities = staff.Capabilities,
            history = historyResults,
            pay,
            sensitiveDataHidden = canSeeSensitive ? null : (bool?)true,
        });
    }
}
