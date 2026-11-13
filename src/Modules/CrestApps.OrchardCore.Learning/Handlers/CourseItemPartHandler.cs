using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Learning.Handlers;

/// <summary>
/// Gives a new course item the kind and default completion mode of its content type, whichever way it is created: the
/// editor, a recipe, the content API or a workflow.
/// </summary>
internal sealed class CourseItemPartHandler : ContentPartHandler<CourseItemPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CourseItemPartHandler(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override async Task InitializingAsync(InitializingContentContext context, CourseItemPart part)
    {
        await ApplyDefaultsAsync(part);
    }

    public override async Task UpdatedAsync(UpdateContentContext context, CourseItemPart part)
    {
        await ApplyDefaultsAsync(part);
    }

    private async Task ApplyDefaultsAsync(CourseItemPart part)
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var settings = type?.Parts.FirstOrDefault(definition => definition.PartDefinition?.Name == nameof(CourseItemPart))?.GetSettings<CourseItemPartSettings>();

        if (settings is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(settings.Kind))
        {
            part.Kind = settings.Kind;
        }

        if (string.IsNullOrEmpty(part.CompletionMode))
        {
            part.CompletionMode = settings.DefaultCompletionMode;
        }
    }
}
