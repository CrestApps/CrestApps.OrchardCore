using System.Net;
using CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// Turns the <c>ltiResourceLink</c> items of an accepted Deep Linking response into External tool course items of the
/// course: published, contained in the course and added to the last section of the course's draft outline, so they
/// reach learners when the course is published. An item with a line item counts toward the grade and completes when
/// the tool posts a fully graded score; one without completes when it is opened.
/// </summary>
public sealed class LtiDeepLinkItemWriter
{
    private readonly IContentManager _contentManager;

    public LtiDeepLinkItemWriter(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<IReadOnlyList<ContentItem>> WriteAsync(ExternalTool tool, string courseId, IEnumerable<LtiDeepLinkItem> items, string sectionTitle)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(items);

        var course = await _contentManager.GetAsync(courseId, VersionOptions.DraftRequired);

        if (course is null)
        {
            return [];
        }

        var created = new List<ContentItem>();

        foreach (var deepLink in items)
        {
            var item = await _contentManager.NewAsync(LtiConstants.ContentTypes.ExternalToolItem);
            var title = string.IsNullOrWhiteSpace(deepLink.Title) ? tool.Name : deepLink.Title;

            item.DisplayText = title;
            item.Alter<TitlePart>(part => part.Title = title);

            if (!string.IsNullOrWhiteSpace(deepLink.Text))
            {
                item.Alter<HtmlBodyPart>(part => part.Html = "<p>" + WebUtility.HtmlEncode(deepLink.Text) + "</p>");
            }

            item.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            item.Alter<ExternalToolItemPart>(part =>
            {
                part.ToolId = tool.ItemId;
                part.TargetLinkUri = deepLink.Url;
                part.CustomParameters = deepLink.CustomParameters;
                part.ScoreMaximum = deepLink.ScoreMaximum;
                part.LineItemLabel = deepLink.LineItemLabel;
                part.LineItemResourceId = deepLink.LineItemResourceId;
                part.LineItemTag = deepLink.LineItemTag;
                part.DeepLinkedContent = deepLink.Json;
            });
            item.Alter<CourseItemPart>(part =>
            {
                part.Kind = LtiConstants.ExternalToolKind;
                part.Gradable = deepLink.ScoreMaximum is not null;
                part.CompletionMode = deepLink.ScoreMaximum is not null ? CompletionModes.Graded : CompletionModes.Viewed;
            });

            await _contentManager.CreateAsync(item, VersionOptions.Published);

            created.Add(item);
        }

        if (created.Count == 0)
        {
            return created;
        }

        course.Alter<CourseOutlinePart>(part =>
        {
            part.Sections ??= [];

            if (part.Sections.Count == 0)
            {
                part.Sections.Add(new CourseSection { Id = IdGenerator.GenerateId(), Title = sectionTitle });
            }

            var section = part.Sections[part.Sections.Count - 1];

            section.Items ??= [];

            foreach (var item in created)
            {
                section.Items.Add(new CourseOutlineItem { ContentItemId = item.ContentItemId });
            }
        });

        await _contentManager.UpdateAsync(course);

        return created;
    }
}
