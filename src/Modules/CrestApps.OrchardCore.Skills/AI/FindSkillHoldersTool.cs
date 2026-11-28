using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Finds the people who hold a skill at or above a proficiency on a day through
/// <see cref="ISkillAssignmentService.FindHoldersAsync"/>, the query scheduling and routing use: the skill must be enabled,
/// the assignment in its dates, and a valid certificate held when the skill requires one. Users and staff members are
/// asked separately and listed with their names. It needs <see cref="SkillsPermissions.ViewSkills"/>.
/// </summary>
internal sealed class FindSkillHoldersTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findSkillHolders";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "skillId": {
          "type": "string",
          "description": "The id of the skill, from listSkills."
        },
        "minimumProficiency": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5,
          "description": "The lowest proficiency that counts, from 1 to 5. Defaults to 1."
        },
        "date": {
          "type": "string",
          "description": "The day the skill must count on, as yyyy-MM-dd. Defaults to today."
        },
        "subjectKind": {
          "type": "string",
          "enum": ["User", "Staff"],
          "description": "Only users or only staff members. Leave it out for both."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many people a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": ["skillId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds the people who hold a skill at or above a proficiency on a date. Use it to answer \"who can do X\".";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ViewSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var skillId = ReadString(arguments, "skillId");

        if (skillId is null)
        {
            return "Give the skillId, from listSkills.";
        }

        var minimum = ReadInt(arguments, "minimumProficiency") ?? SkillProficiency.Minimum;

        if (!SkillProficiency.IsValid(minimum))
        {
            return "minimumProficiency must be from 1 to 5.";
        }

        if (!TryReadDate(arguments, "date", out var date))
        {
            return "date must be written as yyyy-MM-dd.";
        }

        var kindText = ReadString(arguments, "subjectKind");
        var kind = kindText is null ? null : SkillSubjectKind.Parse(kindText);

        if (kind is not null && kind != SkillSubjectKind.User && kind != SkillSubjectKind.Staff)
        {
            return "subjectKind must be User or Staff.";
        }

        var skill = await services.GetRequiredService<ISkillManager>().FindByIdAsync(skillId, cancellationToken);

        if (skill is null)
        {
            return $"There is no skill with the id '{skillId}'. Use listSkills to find it.";
        }

        var day = date ?? await GetTodayAsync(services);
        var assignmentService = services.GetRequiredService<ISkillAssignmentService>();
        var holders = new List<SkillAssignment>();

        foreach (var subjectKind in kind is null ? new[] { SkillSubjectKind.User, SkillSubjectKind.Staff } : new[] { kind })
        {
            holders.AddRange(await assignmentService.FindHoldersAsync(skillId, minimum, day, subjectKind, cancellationToken));
        }

        var names = await GetSubjectNamesAsync(services, holders.Select(holder => holder.Subject), cancellationToken);
        var ordered = holders
            .OrderByDescending(holder => holder.Proficiency)
            .ThenBy(holder => names.GetValueOrDefault(holder.Subject.Key) ?? holder.SubjectId, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var (page, pageSize) = ReadPage(arguments);

        return Json(new
        {
            skillId,
            skillName = skill.Name,
            skillEnabled = skill.Enabled,
            date = SkillsToolResults.Date(day),
            minimumProficiency = minimum,
            items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(holder => new
            {
                subjectKind = holder.SubjectKind,
                subjectId = holder.SubjectId,
                name = names.GetValueOrDefault(holder.Subject.Key),
                proficiency = holder.Proficiency,
                effectiveFrom = SkillsToolResults.Date(holder.EffectiveFrom),
                effectiveTo = SkillsToolResults.Date(holder.EffectiveTo),
            }),
            total = ordered.Count,
            page,
            pageSize,
            truncated = ordered.Count > page * pageSize,
            note = skill.Enabled ? null : "The skill is disabled, so nobody holds it.",
        });
    }
}
