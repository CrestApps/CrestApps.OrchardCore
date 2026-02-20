namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

public class AIDataSourceSettingsViewModel
{
    public bool EnablePreemptiveRag { get; set; }

    public int DefaultStrictness { get; set; }

    public int DefaultTopNDocuments { get; set; }
}
