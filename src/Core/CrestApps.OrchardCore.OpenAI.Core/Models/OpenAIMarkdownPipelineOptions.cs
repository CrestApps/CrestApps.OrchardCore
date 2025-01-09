using Markdig;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIMarkdownPipelineOptions
{
    public readonly MarkdownPipelineBuilder MarkdownPipelineBuilder;

    public OpenAIMarkdownPipelineOptions()
    {
        MarkdownPipelineBuilder = new MarkdownPipelineBuilder();
    }
}
