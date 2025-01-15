using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;

namespace CrestApps.OrchardCore.OpenAI.Core.Markdig;

public sealed class CustomLinkRenderer : HtmlObjectRenderer<LinkInline>
{
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        if (link.IsImage)
        {
            renderer.Write("<img src=\"").Write(link.Url).Write("\" alt=\"").Write(link.Title).Write("\" />");
        }
        else
        {
            renderer.Write("<a href=\"").Write(link.Url).Write("\"");

            if (!string.IsNullOrEmpty(link.Title))
            {
                renderer.Write(" title=\"").Write(link.Title).Write("\"");
            }

            // Add target="_new" to links
            renderer.Write(" target=\"_new\"");

            renderer.Write(">");
            renderer.WriteChildren(link);
            renderer.Write("</a>");
        }
    }
}
