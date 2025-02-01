using CrestApps.OrchardCore.AI.Core.Models;
using Markdig;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIMarkdownService : IAIMarkdownService
{
    private readonly MarkdownPipeline _markdownPipeline;

    public AIMarkdownService(IOptions<AIMarkdownPipelineOptions> options)
    {
        _markdownPipeline = options.Value.MarkdownPipelineBuilder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown, _markdownPipeline);
}
