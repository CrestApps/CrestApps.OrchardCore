using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Recipes;
using CrestApps.OrchardCore.Skills.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Skills.Deployments;

/// <summary>
/// Writes the Skills step: certifications and skills, and the assignments and certificates when asked. Skills carry
/// the name of the certification they require, and assignments carry the skill name, so the step imports on a site
/// whose ids differ.
/// </summary>
internal sealed class SkillsDeploymentSource : DeploymentSourceBase<SkillsDeploymentStep>
{
    private const int PageSize = 500;
    private const int MaxRecords = 50_000;

    private readonly CertificationStore _certificationStore;
    private readonly SkillStore _skillStore;
    private readonly SkillAssignmentStore _assignmentStore;
    private readonly ICertificationRecordService _recordService;

    public SkillsDeploymentSource(
        CertificationStore certificationStore,
        SkillStore skillStore,
        SkillAssignmentStore assignmentStore,
        ICertificationRecordService recordService)
    {
        _certificationStore = certificationStore;
        _skillStore = skillStore;
        _assignmentStore = assignmentStore;
        _recordService = recordService;
    }

    protected override async Task ProcessAsync(SkillsDeploymentStep step, DeploymentPlanResult result)
    {
        var certifications = await _certificationStore.GetAllAsync();
        var skills = await _skillStore.GetAllAsync();
        var certificationNames = certifications.ToDictionary(certification => certification.ItemId, certification => certification.Name, StringComparer.Ordinal);
        var skillNames = skills.ToDictionary(skill => skill.ItemId, skill => skill.Name, StringComparer.Ordinal);

        var node = new JsonObject
        {
            ["name"] = step.Name,
            ["Certifications"] = new JsonArray([.. certifications.Select(certification => Serialize(certification))]),
            ["Skills"] = new JsonArray([.. skills.Select(skill =>
            {
                var item = Serialize(skill);

                if (skill.RequiredCertificationId is { } certificationId && certificationNames.TryGetValue(certificationId, out var certificationName))
                {
                    item["RequiredCertificationName"] = certificationName;
                }

                return item;
            })]),
        };

        if (step.IncludeAssignments)
        {
            var assignments = new JsonArray();

            foreach (var assignment in await _assignmentStore.GetAllAsync())
            {
                var item = new JsonObject
                {
                    ["subjectKind"] = assignment.SubjectKind.ToString(),
                    ["subjectId"] = assignment.SubjectId,
                    ["skillId"] = assignment.SkillId,
                    ["skillName"] = skillNames.GetValueOrDefault(assignment.SkillId),
                    ["proficiency"] = assignment.Proficiency,
                };

                if (assignment.EffectiveFrom is { } from)
                {
                    item["effectiveFrom"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (assignment.EffectiveTo is { } to)
                {
                    item["effectiveTo"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                assignments.Add(item);
            }

            node["Assignments"] = assignments;
        }

        if (step.IncludeCertificationRecords)
        {
            var records = new JsonArray();

            for (var skip = 0; skip < MaxRecords; skip += PageSize)
            {
                var (_, page) = await _recordService.PageAsync(null, skip, PageSize);

                foreach (var record in page)
                {
                    var item = Serialize(record, "remindersRaised", "remindersExpiresOn");

                    if (certificationNames.TryGetValue(record.CertificationId ?? string.Empty, out var certificationName))
                    {
                        item["certificationName"] = certificationName;
                    }

                    records.Add(item);
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }

            node["CertificationRecords"] = records;
        }

        result.Steps.Add(node);
    }

    private static JsonObject Serialize<T>(T record, params string[] without)
    {
        var node = JsonSerializer.SerializeToNode(record, SkillsRecipeStep.JsonOptions).AsObject();

        node.Remove("id");

        foreach (var name in without)
        {
            node.Remove(name);
        }

        return node;
    }
}
