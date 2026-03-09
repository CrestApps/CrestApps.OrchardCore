namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIProfileTemplateSelectionViewModel
{
    public string TemplateId { get; set; }

    public string Source { get; set; }

    public IList<AIProfileTemplateOption> Templates { get; set; } = [];
}

public sealed class AIProfileTemplateOption
{
    public string Id { get; set; }

    public string DisplayText { get; set; }

    public string Category { get; set; }
}
