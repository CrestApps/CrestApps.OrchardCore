using Markdig;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIChatMarkdownService : IAIMarkdownService
{
    private readonly MarkdownPipeline _markdownPipeline;

    public AIChatMarkdownService(IOptions<AIChatMarkdownPipelineOptions> options)
    {
        _markdownPipeline = options.Value.MarkdownPipelineBuilder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown, _markdownPipeline);
}
