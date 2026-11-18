using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Takes a skill away from a person through <see cref="ISkillAssignmentService.RemoveAsync"/>, as clearing a skill on
/// the Skills card does. A skill held through a linked record (a staff member's user) is not removed. A staff member must
/// be among the people the caller manages. It needs <see cref="SkillsPermissions.AssignSkills"/>.
/// </summary>
internal sealed class RemoveSkillAssignmentTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeSkillAssignment";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
    """ + SubjectProperties + """
        ,"skillId": {
          "type": "string",
          "description": "The id of the skill to take away, from listSkills or getSubjectSkills."
        }
      },
      "required": ["subjectKind", "subjectId", "skillId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Takes a skill away from a user or staff member.";

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
            return "Nothing was removed: give the skillId, from getSubjectSkills.";
        }

        var lookup = await FindSubjectAsync(arguments, services, caller, forChange: true, cancellationToken);

        if (lookup.Error is not null)
        {
            return $"Nothing was removed: {lookup.Error}";
        }

        var removed = await services.GetRequiredService<ISkillAssignmentService>().RemoveAsync(lookup.Subject, skillId, caller.Identity?.Name, cancellationToken);

        if (!removed)
        {
            return $"Nothing was removed: {lookup.Name} was not given the skill '{skillId}' directly. Use getSubjectSkills to see their skills; a skill held through their linked user or staff record is removed there.";
        }

        return Json(new
        {
            subject = new { kind = lookup.Subject.Kind, id = lookup.Subject.Id, name = lookup.Name },
            skillId,
            removed,
            message = $"The skill has been taken away from {lookup.Name}.",
        });
    }
}
