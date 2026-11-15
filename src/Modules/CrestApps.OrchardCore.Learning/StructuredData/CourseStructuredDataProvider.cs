using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Seo;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.StructuredData;

/// <summary>
/// Describes a course with schema.org: a <c>Course</c> with its summary, code, level and how long it takes, offered by
/// the site's publisher. It runs after the product provider, so a course sold in the store stays a <c>Course</c> and
/// keeps the offer the product added.
/// </summary>
internal sealed class CourseStructuredDataProvider : ContentPartStructuredDataProvider<CoursePart>
{
    /// <inheritdoc/>
    public override int Order => StructuredDataProviderOrder.Parts + 10;

    /// <inheritdoc/>
    protected override Task BuildAsync(StructuredDataContext context, CoursePart part)
    {
        var entity = context.MainEntity;

        context.SuggestType("Course");

        JsonLd.SetIfNotEmpty(entity, "description", part.Summary);
        JsonLd.SetIfNotEmpty(entity, "courseCode", part.Code);
        JsonLd.SetIfNotEmpty(entity, "educationalLevel", part.Level);

        if (part.EstimatedMinutes is > 0)
        {
            entity["timeRequired"] = "PT" + part.EstimatedMinutes.Value.ToString(CultureInfo.InvariantCulture) + "M";
        }

        if (entity["publisher"] is JsonNode publisher)
        {
            JsonLd.SetIfMissing(entity, "provider", publisher.DeepClone());
        }

        return Task.CompletedTask;
    }
}
