using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;

namespace CrestApps.OrchardCore.AI.Core.Markdig;

public sealed class CustomLinkRenderer : HtmlObjectRenderer<LinkInline>
{
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        if (link.IsImage)
        {
            renderer.Write("<img src=\"").Write(link.Url).Write("\" alt=\"").Write(link.Title).Write("\" />");

            return;
        }

        renderer.Write("<a href=\"").Write(link.Url).Write("\"");

        if (!string.IsNullOrEmpty(link.Title))
        {
            renderer.Write(" title=\"").Write(link.Title).Write("\"");
        }

        renderer.Write(" target=\"_new\"");

        renderer.Write(">");
        renderer.WriteChildren(link);
        renderer.Write("</a>");
    }
}
