using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Skills.Recipes;

/// <summary>
/// Imports the Skills step: certifications and skills (created or updated by id, or by name when the id is new), then
/// the skills people hold and their certificates. A skill or certification can be named instead of given by id, so a
/// recipe written for one site works on another.
/// </summary>
internal sealed class SkillsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly CertificationStore _certificationStore;
    private readonly SkillStore _skillStore;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly ICertificationRecordService _recordService;

    public SkillsRecipeStep(
        CertificationStore certificationStore,
        SkillStore skillStore,
        ISkillAssignmentService assignmentService,
        ICertificationRecordService recordService)
        : base(SkillsConstants.RecipeStepName)
    {
        _certificationStore = certificationStore;
        _skillStore = skillStore;
        _assignmentService = assignmentService;
        _recordService = recordService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var incoming in Read<Certification>(context, "Certifications"))
        {
            if (string.IsNullOrWhiteSpace(incoming.Name))
            {
                context.Errors.Add("A certification in the Skills step has no name.");

                continue;
            }

            var existing = await FindAsync(_certificationStore, incoming.ItemId) ?? await _certificationStore.FindByNameAsync(incoming.Name);

            await SaveAsync(_certificationStore, incoming, existing);
        }

        foreach (var node in Nodes(context, "Skills"))
        {
            var incoming = Deserialize<Skill>(context, node, "Skills");

            if (incoming is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(incoming.Name))
            {
                context.Errors.Add("A skill in the Skills step has no name.");

                continue;
            }

            if (node["RequiredCertificationName"]?.GetValue<string>() is { Length: > 0 } certificationName)
            {
                incoming.RequiredCertificationId = (await _certificationStore.FindByNameAsync(certificationName))?.ItemId;
            }

            var existing = await FindAsync(_skillStore, incoming.ItemId) ?? await _skillStore.FindByNameAsync(incoming.Name);

            await SaveAsync(_skillStore, incoming, existing);
        }

        foreach (var node in Nodes(context, "Assignments"))
        {
            var subject = ReadSubject(node);
            var skillId = await ResolveSkillIdAsync(node);

            if (subject.IsEmpty || skillId is null)
            {
                context.Errors.Add("An assignment in the Skills step needs a subjectId and a skill that exists (skillId or skillName).");

                continue;
            }

            var result = await _assignmentService.AssignAsync(subject, new SkillAssignmentInput
            {
                SkillId = skillId,
                Proficiency = node["proficiency"]?.GetValue<int>() ?? SkillProficiency.Default,
                EffectiveFrom = ReadDate(node, "effectiveFrom"),
                EffectiveTo = ReadDate(node, "effectiveTo"),
            }, "Recipe");

            foreach (var error in result.Errors)
            {
                context.Errors.Add($"An assignment in the Skills step was refused: {error}");
            }
        }

        foreach (var node in Nodes(context, "CertificationRecords"))
        {
            var record = Deserialize<CertificationRecord>(context, node, "CertificationRecords");

            if (record is null)
            {
                continue;
            }

            if (node["certificationName"]?.GetValue<string>() is { Length: > 0 } certificationName)
            {
                record.CertificationId = (await _certificationStore.FindByNameAsync(certificationName))?.ItemId;
            }

            var result = await _recordService.SaveAsync(record, "Recipe");

            foreach (var error in result.Errors)
            {
                context.Errors.Add($"A certificate in the Skills step was refused: {error}");
            }
        }
    }

    private async Task<string> ResolveSkillIdAsync(JsonNode node)
    {
        if (node["skillId"]?.GetValue<string>() is { Length: > 0 } skillId && await _skillStore.FindByIdAsync(skillId) is not null)
        {
            return skillId;
        }

        return node["skillName"]?.GetValue<string>() is { Length: > 0 } skillName
            ? (await _skillStore.FindByNameAsync(skillName))?.ItemId
            : null;
    }

    private static SkillSubject ReadSubject(JsonNode node)
    {
        var kind = Enum.TryParse<SkillSubjectKind>(node["subjectKind"]?.GetValue<string>(), ignoreCase: true, out var parsed)
            ? parsed
            : SkillSubjectKind.User;

        return new SkillSubject(kind, node["subjectId"]?.GetValue<string>());
    }

    private static DateOnly? ReadDate(JsonNode node, string name)
    {
        return DateOnly.TryParse(node[name]?.GetValue<string>(), CultureInfo.InvariantCulture, out var date)
            ? date
            : null;
    }

    private static IEnumerable<JsonObject> Nodes(RecipeExecutionContext context, string section)
    {
        return (context.Step[section] as JsonArray ?? []).OfType<JsonObject>();
    }

    private static IEnumerable<T> Read<T>(RecipeExecutionContext context, string section)
        where T : class
    {
        foreach (var node in Nodes(context, section))
        {
            var incoming = Deserialize<T>(context, node, section);

            if (incoming is not null)
            {
                yield return incoming;
            }
        }
    }

    private static T Deserialize<T>(RecipeExecutionContext context, JsonObject node, string section)
        where T : class
    {
        try
        {
            return node.Deserialize<T>(JsonOptions);
        }
        catch (JsonException ex)
        {
            context.Errors.Add($"An entry of {section} in the Skills step can't be read: {ex.Message}");

            return null;
        }
    }

    private static async Task<T> FindAsync<T, TIndex>(DocumentCatalog<T, TIndex> store, string id)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        return string.IsNullOrEmpty(id) ? null : await store.FindByIdAsync(id);
    }

    private static async Task SaveAsync<T, TIndex>(DocumentCatalog<T, TIndex> store, T incoming, T existing)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        if (existing is null)
        {
            await store.CreateAsync(incoming);

            return;
        }

        // The stored record takes the incoming values, so it stays the same document and keeps its id.
        var id = existing.ItemId;

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(existing, property.GetValue(incoming));
            }
        }

        existing.ItemId = id;
        await store.UpdateAsync(existing);
    }
}
