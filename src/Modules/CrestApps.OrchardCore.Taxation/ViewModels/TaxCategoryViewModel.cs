namespace CrestApps.OrchardCore.Taxation.ViewModels;

public sealed class TaxCategoryViewModel
{
    public bool IsNew { get; set; }

    public string Name { get; set; }

    public string Code { get; set; }

    public string ParentCode { get; set; }

    public string Description { get; set; }
}
