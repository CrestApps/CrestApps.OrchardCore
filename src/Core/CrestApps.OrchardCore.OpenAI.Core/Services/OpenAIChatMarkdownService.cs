using CrestApps.OrchardCore.OpenAI.Core.Models;
using Markdig;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class OpenAIChatMarkdownService : IOpenAIMarkdownService
{
    private readonly MarkdownPipeline _markdownPipeline;

    public OpenAIChatMarkdownService(IOptions<OpenAIChatMarkdownPipelineOptions> options)
    {
        _markdownPipeline = options.Value.MarkdownPipelineBuilder.Build();
    }

    public string ToHtml(string markdown)
        => Markdown.ToHtml(markdown, _markdownPipeline);
}
