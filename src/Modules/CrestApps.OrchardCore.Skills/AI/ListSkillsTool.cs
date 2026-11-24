using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Lists the skill catalog through the skill manager, as the Skills page does, or the certification types through the
/// certification manager, as the Certification Types page does (<c>kind</c>). It needs
/// <see cref="SkillsPermissions.ViewSkills"/>, the catalogs' read permission; the pages themselves need
/// <see cref="SkillsPermissions.ManageSkills"/>.
/// </summary>
internal sealed class ListSkillsTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSkills";

    /// <summary>
    /// The <c>kind</c> value that lists skills.
    /// </summary>
    public const string SkillKind = "skill";

    /// <summary>
    /// The <c>kind</c> value that lists certification types.
    /// </summary>
    public const string CertificationTypeKind = "certificationType";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "kind": {
          "type": "string",
          "enum": ["skill", "certificationType"],
          "description": "What to list: skill (the skill catalog, the default) or certificationType (the certification types people can hold)."
        },
        "name": {
          "type": "string",
          "description": "Part of the name to filter by. Leave it out for every entry."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many entries a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the skill catalog with category, required certification and whether each skill is enabled, or with kind certificationType the certification types people can hold (issuer, validity in months, reminder days, document required); filter by name. Use it to get a skill id or a certification type id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ViewSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var kind = ReadString(arguments, "kind") ?? SkillKind;
        var (page, pageSize) = ReadPage(arguments);
        var name = ReadString(arguments, "name");

        if (string.Equals(kind, CertificationTypeKind, StringComparison.OrdinalIgnoreCase))
        {
            var (typeCount, types) = await services.GetRequiredService<ICertificationManager>().SearchAsync(name, (page - 1) * pageSize, pageSize, cancellationToken);

            return Json(new
            {
                kind = CertificationTypeKind,
                items = types.Select(SkillsToolResults.Certification),
                total = typeCount,
                page,
                pageSize,
                truncated = typeCount > page * pageSize,
            });
        }

        if (!string.Equals(kind, SkillKind, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was listed: kind must be '{SkillKind}' or '{CertificationTypeKind}', not '{kind}'.";
        }

        var (count, skills) = await services.GetRequiredService<ISkillManager>().SearchAsync(name, (page - 1) * pageSize, pageSize, cancellationToken);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);

        return Json(new
        {
            kind = SkillKind,
            items = skills.Select(skill => SkillsToolResults.Skill(skill, certifications)),
            total = count,
            page,
            pageSize,
            truncated = count > page * pageSize,
        });
    }
}
