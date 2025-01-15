using Markdig;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIChatMarkdownPipelineOptions
{
    public readonly MarkdownPipelineBuilder MarkdownPipelineBuilder;

    public OpenAIChatMarkdownPipelineOptions()
    {
        MarkdownPipelineBuilder = new MarkdownPipelineBuilder();
    }
}
