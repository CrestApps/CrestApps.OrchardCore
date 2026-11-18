using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Deletes a skill through the skill manager, as the Skills page's Delete does. The assignments of the skill stay stored
/// but no longer count, because the skill is gone. It needs <see cref="SkillsPermissions.ManageSkills"/> and asks for
/// approval.
/// </summary>
internal sealed class DeleteSkillTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteSkill";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "skillId": {
          "type": "string",
          "description": "The id of the skill to delete, from listSkills."
        }
      },
      "required": ["skillId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a skill from the catalog; nobody holds it any more afterwards. Disable it instead when people still hold it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ManageSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var skillId = ReadString(arguments, "skillId");

        if (skillId is null)
        {
            return "Nothing was deleted: give the skillId, from listSkills.";
        }

        var manager = services.GetRequiredService<ISkillManager>();
        var skill = await manager.FindByIdAsync(skillId, cancellationToken);

        if (skill is null)
        {
            return $"Nothing was deleted: there is no skill with the id '{skillId}'. Use listSkills to find it.";
        }

        var deleted = await manager.DeleteAsync(skill, cancellationToken);

        return Json(new
        {
            id = skill.ItemId,
            name = skill.Name,
            deleted,
            message = deleted ? $"The skill '{skill.Name}' has been deleted. People who held it no longer do." : $"The skill '{skill.Name}' could not be deleted.",
        });
    }
}
