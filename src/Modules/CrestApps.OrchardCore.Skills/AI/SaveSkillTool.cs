using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Creates or changes a skill through the skill manager, whose catalog handler holds the editor's rules (a name is
/// required and unique). The required certification must be an enabled certification type, as the editor offers only
/// those. Only the fields given change. It needs <see cref="SkillsPermissions.ManageSkills"/>.
/// </summary>
internal sealed class SaveSkillTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveSkill";

    private static readonly string[] _fieldNames = ["name", "category", "description", "requiredCertificationId", "enabled"];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "skillId": {
          "type": "string",
          "description": "The id of the skill to change, from listSkills. Leave it out to create a skill."
        },
        "name": {
          "type": "string",
          "description": "The skill's name, unique in the catalog. Required for a new skill."
        },
        "category": {
          "type": "string",
          "description": "A group the skill is listed under, such as Equipment. An empty text removes it."
        },
        "description": {
          "type": "string",
          "description": "What the skill means. An empty text removes it."
        },
        "requiredCertificationId": {
          "type": "string",
          "description": "The id of the certification type, from listCertificationTypes, a person must hold a valid certificate of for the skill to count. An empty text removes the requirement."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the skill is offered and counts. A new skill is enabled when left out."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a skill in the catalog: name, category, description, the certification it requires and enabled. Only the fields given change.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ManageSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ISkillManager>();
        var skillId = ReadString(arguments, "skillId");
        Skill skill;

        if (skillId is null)
        {
            skill = await manager.NewAsync(cancellationToken: cancellationToken);
        }
        else
        {
            skill = await manager.FindByIdAsync(skillId, cancellationToken);

            if (skill is null)
            {
                return $"Nothing was saved: there is no skill with the id '{skillId}'. Use listSkills to find it, or leave skillId out to create one.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }
        }

        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);

        if (Has(arguments, "requiredCertificationId"))
        {
            var certificationId = ReadString(arguments, "requiredCertificationId");

            if (certificationId is not null && certificationId != skill.RequiredCertificationId &&
                (!certifications.TryGetValue(certificationId, out var certification) || !certification.Enabled))
            {
                return $"Nothing was saved: there is no enabled certification type with the id '{certificationId}'. Use listCertificationTypes to find it.";
            }

            skill.RequiredCertificationId = certificationId;
        }

        if (Has(arguments, "name"))
        {
            skill.Name = ReadString(arguments, "name");
        }

        if (Has(arguments, "category"))
        {
            skill.Category = ReadString(arguments, "category");
        }

        if (Has(arguments, "description"))
        {
            skill.Description = ReadString(arguments, "description");
        }

        if (Has(arguments, "enabled"))
        {
            skill.Enabled = ReadBool(arguments, "enabled") ?? skill.Enabled;
        }

        var validation = await manager.ValidateAsync(skill, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        if (skillId is null)
        {
            await manager.CreateAsync(skill, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(skill, cancellationToken: cancellationToken);
        }

        return Json(new
        {
            skill = SkillsToolResults.Skill(skill, certifications),
            created = skillId is null,
            message = skillId is null ? "The skill has been created." : "The skill has been saved.",
        });
    }
}
