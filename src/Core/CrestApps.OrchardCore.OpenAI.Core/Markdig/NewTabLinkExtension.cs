using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;

namespace CrestApps.OrchardCore.OpenAI.Core.Markdig;

public sealed class NewTabLinkExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            htmlRenderer.ObjectRenderers.Replace<LinkInlineRenderer>(new CustomLinkRenderer());
        }
    }
}
