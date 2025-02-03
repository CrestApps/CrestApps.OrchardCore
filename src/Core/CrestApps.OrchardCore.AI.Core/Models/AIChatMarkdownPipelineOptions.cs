using Markdig;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIChatMarkdownPipelineOptions
{
    public readonly MarkdownPipelineBuilder MarkdownPipelineBuilder;

    public AIChatMarkdownPipelineOptions()
    {
        MarkdownPipelineBuilder = new MarkdownPipelineBuilder();
    }
}
