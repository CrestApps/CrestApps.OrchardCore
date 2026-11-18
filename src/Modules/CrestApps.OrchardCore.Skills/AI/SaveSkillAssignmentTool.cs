using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Gives a person a skill, or changes its proficiency and dates, through <see cref="ISkillAssignmentService.AssignAsync"/>
/// as the Skills card of the user and staff editors does; the service checks the skill exists, is enabled, and the
/// proficiency and dates. A skill that requires a certification is given even without a valid certificate, as the
/// service does, but it does not count until one is valid; the result says so. A staff member must be among the people
/// the caller manages. It needs <see cref="SkillsPermissions.AssignSkills"/>.
/// </summary>
internal sealed class SaveSkillAssignmentTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveSkillAssignment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
    """ + SubjectProperties + """
        ,"skillId": {
          "type": "string",
          "description": "The id of the skill, from listSkills."
        },
        "proficiency": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5,
          "description": "How well the person does it, from 1 to 5. Keeps the stored level when left out, or 3 for a new skill."
        },
        "effectiveFrom": {
          "type": "string",
          "description": "The first day the skill counts, as yyyy-MM-dd. An empty text removes it; left out keeps the stored day."
        },
        "effectiveTo": {
          "type": "string",
          "description": "The last day the skill counts, as yyyy-MM-dd. An empty text removes it; left out keeps the stored day."
        }
      },
      "required": ["subjectKind", "subjectId", "skillId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gives a user or staff member a skill, or changes its proficiency (1 to 5) and effective dates. A skill that requires a certification does not count until the person holds a valid certificate of it; the result says whether it counts today.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.AssignSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var skillId = ReadString(arguments, "skillId");

        if (skillId is null)
        {
            return "Nothing was saved: give the skillId, from listSkills.";
        }

        if (!TryReadDate(arguments, "effectiveFrom", out var from) || !TryReadDate(arguments, "effectiveTo", out var to))
        {
            return "Nothing was saved: effectiveFrom and effectiveTo must be dates written as yyyy-MM-dd.";
        }

        if (Has(arguments, "proficiency") && ReadInt(arguments, "proficiency") is null)
        {
            return "Nothing was saved: proficiency must be a whole number from 1 to 5.";
        }

        var lookup = await FindSubjectAsync(arguments, services, caller, forChange: true, cancellationToken);

        if (lookup.Error is not null)
        {
            return $"Nothing was saved: {lookup.Error}";
        }

        var assignmentService = services.GetRequiredService<ISkillAssignmentService>();
        var existing = (await assignmentService.GetAssignmentsAsync(lookup.Subject, cancellationToken)).FirstOrDefault(assignment => assignment.SkillId == skillId);
        var input = new SkillAssignmentInput
        {
            SkillId = skillId,
            Proficiency = ReadInt(arguments, "proficiency") ?? existing?.Proficiency ?? SkillProficiency.Default,
            EffectiveFrom = Has(arguments, "effectiveFrom") ? from : existing?.EffectiveFrom,
            EffectiveTo = Has(arguments, "effectiveTo") ? to : existing?.EffectiveTo,
        };

        var result = await assignmentService.AssignAsync(lookup.Subject, input, caller.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", result.Errors)}";
        }

        var today = await GetTodayAsync(services);
        var skill = await services.GetRequiredService<ISkillManager>().FindByIdAsync(skillId, cancellationToken);
        var counts = await assignmentService.HoldsAsync(lookup.Subject, skillId, input.Proficiency, today, cancellationToken);
        string why = null;

        if (!counts)
        {
            if (input.EffectiveFrom > today || input.EffectiveTo < today)
            {
                why = "Today is outside the skill's effective dates.";
            }
            else if (!string.IsNullOrEmpty(skill?.RequiredCertificationId) &&
                !await services.GetRequiredService<ICertificationRecordService>().IsValidOnAsync(lookup.Subject, skill.RequiredCertificationId, today, cancellationToken))
            {
                var certification = await services.GetRequiredService<ICertificationManager>().FindByIdAsync(skill.RequiredCertificationId, cancellationToken);

                why = $"The skill requires the certification '{certification?.Name ?? skill.RequiredCertificationId}', and the person holds no certificate of it that is valid today. Record one with saveCertificate and verify it with setCertificateStatus.";
            }
        }

        return Json(new
        {
            subject = new { kind = lookup.Subject.Kind, id = lookup.Subject.Id, name = lookup.Name },
            skillId,
            skillName = skill?.Name,
            proficiency = input.Proficiency,
            effectiveFrom = SkillsToolResults.Date(input.EffectiveFrom),
            effectiveTo = SkillsToolResults.Date(input.EffectiveTo),
            created = existing is null,
            countsToday = counts,
            whyNotCounting = why,
            message = existing is null ? $"{lookup.Name} now holds '{skill?.Name}'." : $"{lookup.Name}'s '{skill?.Name}' has been saved.",
        });
    }
}
