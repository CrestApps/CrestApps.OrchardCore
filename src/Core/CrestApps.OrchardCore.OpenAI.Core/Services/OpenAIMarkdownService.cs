using CrestApps.OrchardCore.OpenAI.Core.Models;
using Markdig;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public class OpenAIMarkdownService : IOpenAIMarkdownService
{
    private readonly MarkdownPipeline _markdownPipeline;

    public OpenAIMarkdownService(IOptions<OpenAIMarkdownPipelineOptions> options)
    {
        _markdownPipeline = options.Value.MarkdownPipelineBuilder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown, _markdownPipeline);
}
