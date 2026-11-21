using OrchardCore;
using System.Globalization;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the course outline. It lists the sections, then every item the course contains, so an author places
/// each item in a section, orders it and says whether it counts toward completion. Items are added with Orchard's list
/// of the course; this editor only arranges them.
/// </summary>
internal sealed class CourseOutlinePartDisplayDriver : ContentPartDisplayDriver<CourseOutlinePart>
{
    private const int BlankSections = 2;

    private readonly CourseEditorOptions _editorOptions;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public CourseOutlinePartDisplayDriver(
        CourseEditorOptions editorOptions,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<CourseOutlinePartDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CourseOutlinePart part, BuildPartEditorContext context)
    {
        return Initialize<CourseOutlinePartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.IsNew = context.IsNew;
            model.CourseId = part.ContentItem.ContentItemId;

            var order = 0;

            foreach (var section in part.Sections)
            {
                model.Sections.Add(new CourseOutlineSectionViewModel
                {
                    Id = section.Id,
                    Title = section.Title,
                    Summary = section.Summary,
                    Order = ++order,
                    OpensDaysAfterEnrollment = DaysAfterEnrollment(section),
                });
            }

            for (var index = 0; index < BlankSections; index++)
            {
                model.Sections.Add(new CourseOutlineSectionViewModel { Id = IdGenerator.GenerateId(), Order = ++order });
            }

            if (context.IsNew)
            {
                return;
            }

            var placed = part.Sections
                .SelectMany(section => section.Items.Select((item, position) => (Section: section, Item: item, Position: position)))
                .GroupBy(entry => entry.Item.ContentItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var item in await _editorOptions.GetContainedItemsAsync(model.CourseId))
            {
                var type = await _contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
                var row = new CourseOutlineItemViewModel
                {
                    ContentItemId = item.ContentItemId,
                    Title = item.DisplayText,
                    TypeDisplayName = type?.DisplayName ?? item.ContentType,
                    Published = item.Published,
                };

                if (placed.TryGetValue(item.ContentItemId, out var entry))
                {
                    row.SectionId = entry.Section.Id;
                    row.Order = entry.Position + 1;
                    row.Required = entry.Item.Required;
                }

                model.Items.Add(row);
            }

            var sectionOrder = model.Sections
                .Select((section, position) => (section.Id, Position: position))
                .ToDictionary(entry => entry.Id, entry => entry.Position, StringComparer.Ordinal);

            model.Items = model.Items
                .OrderBy(item => item.SectionId is not null && sectionOrder.TryGetValue(item.SectionId, out var position) ? position : int.MaxValue)
                .ThenBy(item => item.Order)
                .ToList();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(CourseOutlinePart part, UpdatePartEditorContext context)
    {
        var model = new CourseOutlinePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var existing = part.Sections.ToDictionary(section => section.Id, StringComparer.Ordinal);
        var sections = new List<CourseSection>();

        foreach (var row in (model.Sections ?? []).Where(row => !string.IsNullOrWhiteSpace(row?.Title)).OrderBy(row => row.Order))
        {
            var id = string.IsNullOrWhiteSpace(row.Id) ? IdGenerator.GenerateId() : row.Id.Trim();
            var section = existing.TryGetValue(id, out var current) ? current : new CourseSection { Id = id };

            section.Title = row.Title.Trim();
            section.Summary = row.Summary?.Trim();
            section.ReleaseRules = section.ReleaseRules.Where(rule => rule.Name != LearningRuleNames.DaysAfterEnrollment).ToList();

            if (row.OpensDaysAfterEnrollment is > 0)
            {
                var rule = new LearningRuleSettings { Name = LearningRuleNames.DaysAfterEnrollment };

                rule.Values["Days"] = row.OpensDaysAfterEnrollment.Value.ToString(CultureInfo.InvariantCulture);
                section.ReleaseRules.Add(rule);
            }

            section.Items = [];
            sections.Add(section);
        }

        if (model.Items is { Count: > 0 })
        {
            foreach (var row in model.Items.Where(row => !string.IsNullOrEmpty(row?.ContentItemId)).OrderBy(row => row.Order))
            {
                var section = sections.FirstOrDefault(candidate => candidate.Id == row.SectionId);

                if (section is null)
                {
                    continue;
                }

                if (section.Items.Any(item => item.ContentItemId == row.ContentItemId))
                {
                    continue;
                }

                section.Items.Add(new CourseOutlineItem { ContentItemId = row.ContentItemId, Required = row.Required });
            }
        }
        else
        {
            // The items table was not posted, as on a new course: keep the items the sections had.
            foreach (var section in sections)
            {
                if (existing.TryGetValue(section.Id, out var current))
                {
                    section.Items = current.Items;
                }
            }
        }

        part.Sections = sections;

        return Edit(part, context);
    }

    private static int? DaysAfterEnrollment(CourseSection section)
    {
        var value = section.ReleaseRules.FirstOrDefault(rule => rule.Name == LearningRuleNames.DaysAfterEnrollment)?.Get("Days");

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ? days : null;
    }
}
