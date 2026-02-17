namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

public class AIDataSourceSettingsViewModel
{
    public bool EnableEarlyRag { get; set; }

    public int DefaultStrictness { get; set; }

    public int DefaultTopNDocuments { get; set; }
}
