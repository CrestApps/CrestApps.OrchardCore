using System.Globalization;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// A field of the Course type's own part, as the Course Categories feature looks at it.
/// </summary>
/// <param name="Name">The field's name.</param>
/// <param name="FieldType">The field's type, such as <c>TaxonomyField</c>.</param>
/// <param name="TaxonomyId">The taxonomy a taxonomy field uses, or <see langword="null"/>.</param>
public sealed record CourseCategoryFieldCandidate(string Name, string FieldType, string TaxonomyId);

/// <summary>
/// What the Course Categories feature does to the Course type and the taxonomies.
/// </summary>
/// <param name="FieldName">The category field's name.</param>
/// <param name="TaxonomyId">The taxonomy the field uses.</param>
/// <param name="AddField">Whether the field is added.</param>
/// <param name="SetFieldTaxonomy">Whether an existing field is pointed at the taxonomy.</param>
/// <param name="CreateTaxonomy">Whether the taxonomy is created.</param>
public sealed record CourseCategoryPlan(string FieldName, string TaxonomyId, bool AddField, bool SetFieldTaxonomy, bool CreateTaxonomy);

/// <summary>
/// Decides how the Course Categories feature sets up a site, keeping a category field and a taxonomy the site already
/// has: a taxonomy field on the Course type's own part (the one named <c>Category</c> first), and a taxonomy titled
/// "Course categories" an administrator made by hand.
/// </summary>
public static class CourseCategorySetup
{
    /// <summary>
    /// The type name of Orchard Core's taxonomy field.
    /// </summary>
    public const string TaxonomyFieldType = "TaxonomyField";

    private const string AlternativeFieldName = "CourseCategory";

    /// <summary>
    /// Decides the plan.
    /// </summary>
    /// <param name="fields">The fields of the Course type's own part.</param>
    /// <param name="existingTaxonomyId">A taxonomy titled "Course categories" the site already has, or <see langword="null"/>.</param>
    /// <param name="newTaxonomyId">The id to give a taxonomy the feature creates.</param>
    /// <returns>The plan.</returns>
    public static CourseCategoryPlan Decide(IEnumerable<CourseCategoryFieldCandidate> fields, string existingTaxonomyId, string newTaxonomyId)
    {
        var list = fields?.Where(field => field is not null).ToList() ?? [];
        var create = string.IsNullOrEmpty(existingTaxonomyId);
        var taxonomyId = create ? newTaxonomyId : existingTaxonomyId;

        if (Pick(list) is { } chosen)
        {
            return string.IsNullOrEmpty(chosen.TaxonomyId)
                ? new CourseCategoryPlan(chosen.Name, taxonomyId, AddField: false, SetFieldTaxonomy: true, CreateTaxonomy: create)
                : new CourseCategoryPlan(chosen.Name, chosen.TaxonomyId, AddField: false, SetFieldTaxonomy: false, CreateTaxonomy: false);
        }

        var name = LearningConstants.CourseCategories.FieldName;
        var number = 1;

        while (list.Any(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = number == 1 ? AlternativeFieldName : AlternativeFieldName + number.ToString(CultureInfo.InvariantCulture);
            number++;
        }

        return new CourseCategoryPlan(name, taxonomyId, AddField: true, SetFieldTaxonomy: false, CreateTaxonomy: create);
    }

    /// <summary>
    /// Picks the category field among the fields of the Course type's own part: the taxonomy field named
    /// <c>Category</c>, else the first taxonomy field with a taxonomy, else the first taxonomy field.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The field, or <see langword="null"/> when the part has no taxonomy field.</returns>
    public static CourseCategoryFieldCandidate Pick(IEnumerable<CourseCategoryFieldCandidate> fields)
    {
        var taxonomyFields = (fields ?? []).Where(field => field?.FieldType == TaxonomyFieldType).ToList();

        return taxonomyFields.FirstOrDefault(field => string.Equals(field.Name, LearningConstants.CourseCategories.FieldName, StringComparison.Ordinal))
            ?? taxonomyFields.FirstOrDefault(field => !string.IsNullOrEmpty(field.TaxonomyId))
            ?? taxonomyFields.FirstOrDefault();
    }
}
