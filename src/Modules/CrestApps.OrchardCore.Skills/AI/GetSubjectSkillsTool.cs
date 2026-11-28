using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Returns a person's skills and certificates through the assignment and certificate services: the skills given to
/// them, whether each counts today (an enabled skill, in its dates, with a valid certificate when it requires one), the
/// skills that count through a linked record (a staff member and their user are one skill holder) and their
/// certificates. It needs <see cref="SkillsPermissions.ViewSkills"/>.
/// </summary>
internal sealed class GetSubjectSkillsTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getSubjectSkills";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
    """ + SubjectProperties + """
      },
      "required": ["subjectKind", "subjectId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the skills a person holds (proficiency and effective dates, and whether each counts today) and their certificates, for a user or a staff member.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ViewSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var lookup = await FindSubjectAsync(arguments, services, caller, forChange: false, cancellationToken);

        if (lookup.Error is not null)
        {
            return lookup.Error;
        }

        var assignmentService = services.GetRequiredService<ISkillAssignmentService>();
        var today = await GetTodayAsync(services);
        var skills = await SkillsToolResults.GetSkillsAsync(services, cancellationToken);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);
        var own = await assignmentService.GetAssignmentsAsync(lookup.Subject, cancellationToken);
        var effective = await assignmentService.GetEffectiveAsync(lookup.Subject, today, cancellationToken);
        var certificates = await services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(lookup.Subject, cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal) { [lookup.Subject.Key] = lookup.Name };
        var certificateResults = new List<object>();

        foreach (var record in certificates)
        {
            certificateResults.Add(await SkillsToolResults.CertificateAsync(services, record, certifications, names, today));
        }

        return Json(new
        {
            subject = new { kind = lookup.Subject.Kind, id = lookup.Subject.Id, name = lookup.Name },
            today = SkillsToolResults.Date(today),
            skills = own.OrderBy(assignment => skills.TryGetValue(assignment.SkillId, out var skill) ? skill.Name : assignment.SkillId, StringComparer.CurrentCultureIgnoreCase).Select(assignment => new
            {
                skillId = assignment.SkillId,
                skillName = skills.TryGetValue(assignment.SkillId, out var skill) ? skill.Name : null,
                proficiency = assignment.Proficiency,
                effectiveFrom = SkillsToolResults.Date(assignment.EffectiveFrom),
                effectiveTo = SkillsToolResults.Date(assignment.EffectiveTo),
                countsToday = effective.Any(held => held.SkillId == assignment.SkillId && held.Subject == lookup.Subject),
            }),
            throughLinkedRecord = effective.Where(held => held.Subject != lookup.Subject).Select(held => new
            {
                skillId = held.SkillId,
                skillName = skills.TryGetValue(held.SkillId, out var skill) ? skill.Name : null,
                proficiency = held.Proficiency,
                heldAs = held.SubjectKind,
                heldById = held.SubjectId,
            }),
            certificates = certificateResults,
        });
    }
}
