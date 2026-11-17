using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.KnowledgeBase.Drivers;

/// <summary>
/// Edits an article's audience, category and summary, and shows the "was this helpful?" question under it.
/// </summary>
internal sealed class KnowledgeArticlePartDisplayDriver : ContentPartDisplayDriver<KnowledgeArticlePart>
{
    public override IDisplayResult Display(KnowledgeArticlePart part, BuildPartDisplayContext context)
        => Initialize<KnowledgeArticleFeedbackViewModel>("KnowledgeArticleFeedback", model =>
        {
            model.ContentItemId = part.ContentItem.ContentItemId;
            model.HelpfulCount = part.HelpfulCount;
        }).Location("Detail", "Content:20");

    public override IDisplayResult Edit(KnowledgeArticlePart part, BuildPartEditorContext context)
        => Initialize<KnowledgeArticlePartViewModel>("KnowledgeArticlePart_Edit", model =>
        {
            model.Audience = part.Audience;
            model.Summary = part.Summary;
        });

    public override async Task<IDisplayResult> UpdateAsync(KnowledgeArticlePart part, UpdatePartEditorContext context)
    {
        var model = new KnowledgeArticlePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Audience = model.Audience;
        part.Summary = string.IsNullOrWhiteSpace(model.Summary) ? null : model.Summary.Trim();

        return Edit(part, context);
    }
}
