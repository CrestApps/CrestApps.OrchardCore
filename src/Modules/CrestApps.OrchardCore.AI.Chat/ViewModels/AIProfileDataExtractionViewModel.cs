namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIProfileDataExtractionViewModel
{
    public bool EnableDataExtraction { get; set; }

    public int ExtractionCheckInterval { get; set; } = 1;

    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;

    public List<DataExtractionEntryViewModel> Entries { get; set; } = [];
}

public class DataExtractionEntryViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool AllowMultipleValues { get; set; }

    public bool IsUpdatable { get; set; }
}
