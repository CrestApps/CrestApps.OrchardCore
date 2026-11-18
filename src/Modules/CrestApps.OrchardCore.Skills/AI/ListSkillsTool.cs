using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Lists the skill catalog through the skill manager, as the Skills page does. It needs
/// <see cref="SkillsPermissions.ViewSkills"/>, the catalog's read permission; the Skills page itself needs
/// <see cref="SkillsPermissions.ManageSkills"/>.
/// </summary>
internal sealed class ListSkillsTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSkills";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Part of the skill's name to filter by. Leave it out for every skill."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many skills a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the skill catalog with category, required certification and whether each skill is enabled; filter by name. Use it to get a skill id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ViewSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (page, pageSize) = ReadPage(arguments);
        var (count, skills) = await services.GetRequiredService<ISkillManager>().SearchAsync(ReadString(arguments, "name"), (page - 1) * pageSize, pageSize, cancellationToken);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);

        return Json(new
        {
            items = skills.Select(skill => SkillsToolResults.Skill(skill, certifications)),
            total = count,
            page,
            pageSize,
            truncated = count > page * pageSize,
        });
    }
}
