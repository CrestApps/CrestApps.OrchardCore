using Markdig;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIChatMarkdownPipelineOptions
{
    public readonly MarkdownPipelineBuilder MarkdownPipelineBuilder;

    public AIChatMarkdownPipelineOptions()
    {
        MarkdownPipelineBuilder = new MarkdownPipelineBuilder();
    }
}
