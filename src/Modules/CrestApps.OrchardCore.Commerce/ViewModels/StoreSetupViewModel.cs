namespace CrestApps.OrchardCore.Commerce.ViewModels;

public class StoreSetupViewModel
{
    public IList<StoreSetupStepViewModel> Steps { get; } = [];

    /// <summary>
    /// Gets or sets the step to do now, opened on the page.
    /// </summary>
    public string Current { get; set; }
}

public class StoreSetupStepViewModel
{
    public string Name { get; set; }

    public StoreSetupStepDescription Description { get; set; }

    public bool IsDone { get; set; }

    public bool IsSkipped { get; set; }
}
