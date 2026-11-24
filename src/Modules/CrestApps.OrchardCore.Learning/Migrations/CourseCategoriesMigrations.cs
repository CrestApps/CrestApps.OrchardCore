using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using OrchardCore.Taxonomies.Models;
using OrchardCore.Taxonomies.Settings;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Migrations;

/// <summary>
/// Gives courses a category tree from an Orchard Core taxonomy: the "Course categories" taxonomy, a Category taxonomy
/// field on the Course type's own part, and the taxonomy in the filters of the admin content list. A site that already
/// has a taxonomy field on courses keeps it and its taxonomy; a site that already has a taxonomy titled "Course
/// categories" (such as one made by hand) gets the field pointed at it instead of a second taxonomy.
/// </summary>
internal sealed class CourseCategoriesMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCategoriesMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="siteService">The site settings.</param>
    public CourseCategoriesMigrations(IContentDefinitionManager contentDefinitionManager, ISession session, ISiteService siteService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _siteService = siteService;
    }

    /// <summary>
    /// Adds the term type, the field, the taxonomy and the admin list filter.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(LearningConstants.CourseCategories.TermContentType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.CourseCategories.TermContentType, type => type
                .WithDisplayName("Course category")
                .Listable(false)
                .Creatable(false)
                .WithPart(nameof(TitlePart), part => part
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired })));
        }

        var course = await _contentDefinitionManager.LoadTypeDefinitionAsync(LearningConstants.ContentTypes.Course);
        var fields = course?.Parts
            .FirstOrDefault(part => part.Name == LearningConstants.ContentTypes.Course)?
            .PartDefinition.Fields
            .Select(field => new CourseCategoryFieldCandidate(
                field.Name,
                field.FieldDefinition.Name,
                field.FieldDefinition.Name == CourseCategorySetup.TaxonomyFieldType ? field.GetSettings<TaxonomyFieldSettings>()?.TaxonomyContentItemId : null))
            .ToList() ?? [];

        var existing = await _session.QueryIndex<ContentItemIndex>(index =>
                index.ContentType == "Taxonomy" &&
                index.Latest &&
                index.DisplayText == LearningConstants.CourseCategories.TaxonomyName)
            .FirstOrDefaultAsync();

        var plan = CourseCategorySetup.Decide(fields, existing?.ContentItemId, IdGenerator.GenerateId());

        if (plan.AddField)
        {
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.Course, part => part
                .WithField(plan.FieldName, field => field
                    .OfType(CourseCategorySetup.TaxonomyFieldType)
                    .WithDisplayName("Category")
                    .WithPosition("2")
                    .WithSettings(new TaxonomyFieldSettings
                    {
                        TaxonomyContentItemId = plan.TaxonomyId,
                        Unique = true,
                        Hint = "The category the course is listed in, in the course catalog and in the admin list of courses.",
                    })));
        }
        else if (plan.SetFieldTaxonomy)
        {
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.Course, part => part
                .WithField(plan.FieldName, field => field
                    .MergeSettings<TaxonomyFieldSettings>(settings => settings.TaxonomyContentItemId = plan.TaxonomyId)));
        }

        await AddAdminListFilterAsync(plan.TaxonomyId);

        if (plan.CreateTaxonomy)
        {
            // The taxonomy is a content item, made through the content manager once the migration is committed.
            var taxonomyId = plan.TaxonomyId;
            ShellScope.AddDeferredTask(scope => CreateTaxonomyAsync(scope.ServiceProvider, taxonomyId));
        }

        return 1;
    }

    private async Task AddAdminListFilterAsync(string taxonomyId)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<TaxonomyContentsAdminListSettings>();

        if (settings.TaxonomyContentItemIds.Contains(taxonomyId, StringComparer.Ordinal))
        {
            return;
        }

        settings.TaxonomyContentItemIds = [.. settings.TaxonomyContentItemIds, taxonomyId];
        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);
    }

    private static async Task CreateTaxonomyAsync(IServiceProvider services, string taxonomyId)
    {
        var contentManager = services.GetRequiredService<IContentManager>();

        if (await contentManager.GetAsync(taxonomyId, VersionOptions.Latest) is not null)
        {
            return;
        }

        var taxonomy = await contentManager.NewAsync("Taxonomy");
        taxonomy.ContentItemId = taxonomyId;
        taxonomy.DisplayText = LearningConstants.CourseCategories.TaxonomyName;
        taxonomy.Alter<TitlePart>(part => part.Title = LearningConstants.CourseCategories.TaxonomyName);
        taxonomy.Alter<TaxonomyPart>(part => part.TermContentType = LearningConstants.CourseCategories.TermContentType);

        // The alias and the path are given, not computed from the title: the deferred task runs without a request, which
        // the Liquid patterns of the alias and the path need.
        ((JsonObject)taxonomy.Content)["AliasPart"] = new JsonObject { ["Alias"] = LearningConstants.CourseCategories.TaxonomyAlias };
        taxonomy.Alter<AutoroutePart>(part => part.Path = LearningConstants.CourseCategories.TaxonomyAlias);
        await contentManager.CreateAsync(taxonomy, VersionOptions.Published);
    }
}
