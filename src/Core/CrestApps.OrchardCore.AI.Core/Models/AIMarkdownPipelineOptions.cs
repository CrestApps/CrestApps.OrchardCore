using Markdig;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIMarkdownPipelineOptions
{
    public readonly MarkdownPipelineBuilder MarkdownPipelineBuilder;

    public AIMarkdownPipelineOptions()
    {
        MarkdownPipelineBuilder = new MarkdownPipelineBuilder();
    }
}
