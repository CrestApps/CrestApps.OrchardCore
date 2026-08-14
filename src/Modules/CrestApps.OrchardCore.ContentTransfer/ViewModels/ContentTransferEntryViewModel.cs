using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentTransferEntryViewModel : ShapeViewModel
{
    public ContentTransferEntry ContentTransferEntry { get; set; }

    public ContentTransferEntryViewModel()
    {
    }

    public ContentTransferEntryViewModel(ContentTransferEntry entry)
    {
        ContentTransferEntry = entry;
    }
}
