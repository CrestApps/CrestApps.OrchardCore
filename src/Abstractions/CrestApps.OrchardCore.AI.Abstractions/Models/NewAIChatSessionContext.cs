namespace CrestApps.OrchardCore.AI.Models;

public class NewAIChatSessionContext
{
    public static readonly NewAIChatSessionContext Robots = new()
    {
        AllowRobots = true
    };

    public bool AllowRobots { get; set; }
}
