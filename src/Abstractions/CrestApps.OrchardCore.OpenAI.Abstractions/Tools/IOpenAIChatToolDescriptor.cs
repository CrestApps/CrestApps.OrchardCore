using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Tools;

public interface IOpenAIChatToolDescriptor
{
    string Name { get; }

    string Description { get; }

    AITool Tool { get; }
}
