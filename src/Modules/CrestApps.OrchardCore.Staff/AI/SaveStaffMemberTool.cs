using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Creates or changes a staff member's core record and team memberships, with the staff editor's rules
/// (<see cref="StaffMemberEditorRules"/>, <see cref="StaffTeamMemberships"/>) and the staff manager's validation (unique
/// employee number and user, known teams and time zone, a manager who exists and is not in a loop). Like the editor, it
/// needs a staff scope (<see cref="StaffPermissions.ManageStaff"/>, or <see cref="StaffPermissions.ManageOwnStaff"/> for the
/// people one manages), adds people only to teams the caller manages, and changes the date of birth only with
/// <see cref="StaffPermissions.ViewSensitiveStaffData"/>. Pay, badge, skills and the cards other modules add to the editor
/// are not changed.
/// </summary>
internal sealed class SaveStaffMemberTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveStaffMember";

    private static readonly string[] _fieldNames =
    [
        "displayName", "email", "userName", "phone", "employeeNumber", "externalId", "jobTitle", "employmentType", "status",
        "hireDate", "seniorityDate", "terminationDate", "dateOfBirth", "managerId", "timeZoneId", "workLocation", "homeBase",
        "contractedHoursPerWeek", "capabilities", "teamMemberships",
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "staffMemberId": {
          "type": "string",
          "description": "The id of the staff member to change, from getStaffMember. Leave it out to add a staff member."
        },
        "displayName": {
          "type": "string",
          "description": "The person's name. Required for a person without a user account; a person with one takes it from the account."
        },
        "email": {
          "type": "string",
          "description": "The work e-mail of a person without a user account; a person with one takes it from the account."
        },
        "userName": {
          "type": "string",
          "description": "The user account to link, by user name, e-mail or user id. A staff member is linked to an account once; it can't be changed afterwards."
        },
        "phone": { "type": "string", "description": "The work phone. An empty text removes it." },
        "employeeNumber": { "type": "string", "description": "The employee (badge) number, unique. An empty text removes it." },
        "externalId": { "type": "string", "description": "The person's id in an outside HR or payroll system. An empty text removes it." },
        "jobTitle": { "type": "string", "description": "The job title, such as Field Technician. An empty text removes it." },
        "employmentType": {
          "type": "string",
          "enum": ["FullTime", "PartTime", "Seasonal", "Temporary", "Contractor", "Subcontractor"],
          "description": "How the person is employed. A new staff member is FullTime when left out."
        },
        "status": {
          "type": "string",
          "enum": ["Active", "OnLeave", "Suspended", "Terminated"],
          "description": "Whether the person is working. Terminated starts the offboarding of other modules (shifts, queues, access)."
        },
        "hireDate": { "type": "string", "description": "The day the person was hired, as yyyy-MM-dd. An empty text removes it." },
        "seniorityDate": { "type": "string", "description": "The day seniority counts from, when not the hire date, as yyyy-MM-dd. An empty text removes it." },
        "terminationDate": { "type": "string", "description": "The person's last working day, as yyyy-MM-dd. An empty text removes it." },
        "dateOfBirth": { "type": "string", "description": "The date of birth, as yyyy-MM-dd. Only for people allowed to see sensitive staff data." },
        "managerId": { "type": "string", "description": "The id of the staff member the person reports to, from getStaffMember. An empty text removes the manager." },
        "timeZoneId": { "type": "string", "description": "The person's IANA time zone, such as America/Phoenix. An empty text uses the main team's, then the site's." },
        "workLocation": { "type": "string", "description": "The place the person works at, such as an office, store or depot. An empty text removes it." },
        "homeBase": { "type": "string", "description": "The address the person starts their day from, for field work. An empty text removes it." },
        "contractedHoursPerWeek": { "type": "number", "description": "The hours a week of the person's contract." },
        "capabilities": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The kinds of work the person does, by name from listStaffCapabilities, such as Technician. The list replaces the offered capabilities the person has; ones a module set and doesn't offer stay."
        },
        "teamMemberships": {
          "type": "array",
          "description": "The teams the person belongs to, from listTeams; the list replaces the stored one and the first becomes the main team. A manager can only add people to the teams they manage.",
          "items": {
            "type": "object",
            "properties": {
              "teamId": { "type": "string", "description": "The team's id, from listTeams." },
              "role": { "type": "string", "enum": ["Member", "Lead", "Scheduler", "Manager"], "description": "The role in the team. Member when left out." },
              "effectiveFrom": { "type": "string", "description": "The first day of the membership, as yyyy-MM-dd." },
              "effectiveTo": { "type": "string", "description": "The last day of the membership, as yyyy-MM-dd." }
            },
            "required": ["teamId"],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a staff member or updates one: job title, employment type, status, dates, manager, time zone, locations, contracted hours, capabilities and team memberships; can link a user account once. Setting a termination date or status Terminated starts offboarding in other modules. Pay, badge and skills have their own tools.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => ManagesAnybodyAsync(services, caller, cancellationToken);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IStaffMemberManager>();
        var scopeService = services.GetRequiredService<IStaffScopeService>();
        var staffMemberId = ReadString(arguments, "staffMemberId");
        var isNew = staffMemberId is null;
        StaffMember staff;

        if (isNew)
        {
            staff = await manager.NewAsync(cancellationToken: cancellationToken);
        }
        else
        {
            staff = await manager.FindByIdAsync(staffMemberId, cancellationToken);

            if (staff is null)
            {
                return $"Nothing was saved: there is no staff member with the id '{staffMemberId}'. Use getStaffMember to find them, or leave staffMemberId out to add one.";
            }

            if (!await scopeService.CanManageAsync(caller, staff, cancellationToken))
            {
                return $"Nothing was saved: you are not allowed to change {staff.DisplayName}: they are not among the people you manage.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }
        }

        var ignored = new List<string>();

        // The user account: linked once, never changed.
        if (ReadString(arguments, "userName") is { } userName)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByIdAsync(userName)
                ?? await userManager.FindByNameAsync(userName)
                ?? (userName.Contains('@') ? await userManager.FindByEmailAsync(userName) : null);

            if (user is null)
            {
                return $"Nothing was saved: there is no user '{userName}'. Give a user name, e-mail or user id.";
            }

            var userId = await userManager.GetUserIdAsync(user);

            if (!string.IsNullOrEmpty(staff.UserId) && staff.UserId != userId)
            {
                return "Nothing was saved: a staff member's user account can't be changed once linked, so their pay and history never move to someone else. Add a new staff member for the other account.";
            }

            StaffMemberEditorRules.LinkUser(staff, userId);
        }

        if (string.IsNullOrEmpty(staff.UserId))
        {
            StaffMemberEditorRules.SetNameAndEmail(
                staff,
                Has(arguments, "displayName") ? ReadString(arguments, "displayName") : staff.DisplayName,
                Has(arguments, "email") ? ReadString(arguments, "email") : staff.Email);
        }
        else
        {
            ignored.AddRange(new[] { "displayName", "email" }.Where(name => Has(arguments, name)));
        }

        if (Has(arguments, "dateOfBirth"))
        {
            if (!await HasAsync(services, caller, StaffPermissions.ViewSensitiveStaffData))
            {
                return "Nothing was saved: you are not allowed to change the date of birth, which is sensitive staff data. Leave dateOfBirth out.";
            }

            if (!TryParseDate(ReadString(arguments, "dateOfBirth"), out var dateOfBirth))
            {
                return "Nothing was saved: dateOfBirth must be a date written as yyyy-MM-dd.";
            }

            staff.DateOfBirth = dateOfBirth;
        }

        foreach (var (name, apply) in new (string, Action<DateOnly?>)[]
        {
            ("hireDate", value => staff.HireDate = value),
            ("seniorityDate", value => staff.SeniorityDate = value),
            ("terminationDate", value => staff.TerminationDate = value),
        })
        {
            if (!Has(arguments, name))
            {
                continue;
            }

            if (!TryParseDate(ReadString(arguments, name), out var date))
            {
                return $"Nothing was saved: {name} must be a date written as yyyy-MM-dd.";
            }

            apply(date);
        }

        if (Has(arguments, "employmentType"))
        {
            if (!Enum.TryParse<EmploymentType>(ReadString(arguments, "employmentType"), ignoreCase: true, out var employmentType) || !Enum.IsDefined(employmentType))
            {
                return "Nothing was saved: employmentType must be FullTime, PartTime, Seasonal, Temporary, Contractor or Subcontractor.";
            }

            staff.EmploymentType = employmentType;
        }

        if (Has(arguments, "status"))
        {
            if (!Enum.TryParse<StaffStatus>(ReadString(arguments, "status"), ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                return "Nothing was saved: status must be Active, OnLeave, Suspended or Terminated.";
            }

            staff.Status = status;
        }

        if (Has(arguments, "contractedHoursPerWeek"))
        {
            var hours = ReadString(arguments, "contractedHoursPerWeek");

            if (hours is null)
            {
                staff.ContractedHoursPerWeek = null;
            }
            else if (!decimal.TryParse(hours, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                return "Nothing was saved: contractedHoursPerWeek must be a number of hours of zero or more.";
            }
            else
            {
                staff.ContractedHoursPerWeek = value;
            }
        }

        SetText(arguments, "phone", value => staff.Phone = value);
        SetText(arguments, "employeeNumber", value => staff.EmployeeNumber = value);
        SetText(arguments, "externalId", value => staff.ExternalId = value);
        SetText(arguments, "jobTitle", value => staff.JobTitle = value);
        SetText(arguments, "managerId", value => staff.ManagerId = value);
        SetText(arguments, "timeZoneId", value => staff.TimeZoneId = value);
        SetText(arguments, "workLocation", value => staff.WorkLocation = value);

        if (Has(arguments, "homeBase"))
        {
            StaffMemberEditorRules.SetHomeBase(staff, ReadString(arguments, "homeBase"));
        }

        if (Has(arguments, "capabilities"))
        {
            var offered = await services.GetRequiredService<IStaffCapabilityService>().ListAsync(cancellationToken: cancellationToken);
            var chosen = new List<string>();

            foreach (var item in ReadStrings(arguments, "capabilities"))
            {
                var match = offered.FirstOrDefault(capability =>
                    string.Equals(capability.Name, item, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(capability.DisplayName, item, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    return $"Nothing was saved: '{item}' is not a capability offered here. Offered: {string.Join(", ", offered.Select(capability => capability.Name))}. {services.GetService<StaffCapabilityToolScope>()?.Note()}".TrimEnd();
                }

                chosen.Add(match.Name);
            }

            StaffMemberEditorRules.SetCapabilities(staff, offered.Select(capability => capability.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), chosen);
        }

        if (Has(arguments, "teamMemberships"))
        {
            if (!services.GetRequiredService<StaffTeamReader>().UsesTeams)
            {
                return "Nothing was saved: team memberships can't be set because the Teams feature is not enabled.";
            }

            var (memberships, membershipError) = ReadMemberships(arguments);

            if (membershipError is not null)
            {
                return $"Nothing was saved: {membershipError}";
            }

            var scope = await scopeService.GetScopeAsync(caller, cancellationToken);

            if (StaffTeamMemberships.AddsTeamOutsideScope(scope, staff, memberships))
            {
                return "Nothing was saved: you can only add people to the teams you manage.";
            }

            StaffTeamMemberships.Apply(staff, memberships);
        }

        var validation = await manager.ValidateAsync(staff, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        if (isNew)
        {
            await manager.CreateAsync(staff, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(staff, cancellationToken: cancellationToken);
        }

        return Json(new
        {
            id = staff.ItemId,
            displayName = staff.DisplayName,
            userId = staff.UserId,
            jobTitle = staff.JobTitle,
            status = staff.Status.ToString(),
            employmentType = staff.EmploymentType.ToString(),
            managerId = staff.ManagerId,
            mainTeamId = staff.PrimaryTeamId,
            teamMemberships = staff.TeamMemberships.Select(membership => new
            {
                teamId = membership.TeamId,
                role = membership.Role.ToString(),
                effectiveFrom = Date(membership.EffectiveFrom),
                effectiveTo = Date(membership.EffectiveTo),
            }),
            capabilities = staff.Capabilities,
            created = isNew,
            ignoredFields = ignored.Count > 0 ? ignored : null,
            note = ignored.Count > 0 ? "A person with a user account takes their name and e-mail from the account." : null,
            message = isNew ? $"{staff.DisplayName} has been added." : $"{staff.DisplayName} has been saved.",
        });
    }

    private static void SetText(AIFunctionArguments arguments, string name, Action<string> apply)
    {
        if (Has(arguments, name))
        {
            apply(ReadString(arguments, name));
        }
    }

    private static List<string> ReadStrings(AIFunctionArguments arguments, string name)
    {
        var element = ReadElement(arguments, name);

        if (element is not { } value)
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        return (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static (List<TeamMembership> Memberships, string Error) ReadMemberships(AIFunctionArguments arguments)
    {
        var memberships = new List<TeamMembership>();

        if (ReadElement(arguments, "teamMemberships") is not { } value || value.ValueKind == JsonValueKind.Null)
        {
            return (memberships, null);
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return (null, "teamMemberships must be a list of { teamId, role, effectiveFrom, effectiveTo }.");
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return (null, "teamMemberships must be a list of { teamId, role, effectiveFrom, effectiveTo }.");
            }

            var teamId = Text(item, "teamId");

            if (teamId is null)
            {
                return (null, "each team membership needs a teamId, from listTeams.");
            }

            var role = TeamMemberRole.Member;

            if (Text(item, "role") is { } roleText && (!Enum.TryParse(roleText, ignoreCase: true, out role) || !Enum.IsDefined(role)))
            {
                return (null, "a team membership's role must be Member, Lead, Scheduler or Manager.");
            }

            if (!TryParseDate(Text(item, "effectiveFrom"), out var from) || !TryParseDate(Text(item, "effectiveTo"), out var to))
            {
                return (null, "a team membership's dates must be written as yyyy-MM-dd.");
            }

            memberships.Add(new TeamMembership { TeamId = teamId, Role = role, EffectiveFrom = from, EffectiveTo = to });
        }

        return (memberships, null);
    }

    private static string Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
