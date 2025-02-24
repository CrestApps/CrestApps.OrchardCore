namespace CrestApps.OrchardCore.AI;

public class AIToolDefinitionEntry
{
    public AIToolDefinitionEntry(Type type)
    {
        ToolType = type;
    }

    public Type ToolType { get; }

    public string Title { get; set; }

    public string Description { get; set; }
}
